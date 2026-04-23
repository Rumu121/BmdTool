using System.Drawing;

namespace BMDEditor.Bmd;

internal static class BmdCodec
{
    private const int C2HeaderMagic = 1012;
    private const int C2SectionMagic = 1001;
    private const int C1HeaderMagic = 25;
    private const int C1SectionMagic = 10;
    private const int EmptyRowOffset = 0x3FFFFF;
    private const int EmptyRowIndent = 0x3FF;

    public static BmdDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        var magic = reader.ReadInt32();
        stream.Position = 0;

        return magic switch
        {
            C2HeaderMagic => LoadCultures2(stream, path),
            C1HeaderMagic => BmdCodecCultures1.Load(stream, path),
            _ => throw new InvalidDataException($"Nieobslugiwany naglowek BMD: {magic}.")
        };
    }

    public static void Save(BmdDocument document, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        switch (document.GameFormat)
        {
            case BmdGameFormat.Cultures1:
                BmdCodecCultures1.Save(document, writer);
                break;
            case BmdGameFormat.Cultures2:
                SaveCultures2(document, writer);
                break;
            default:
                throw new InvalidDataException($"Nieobslugiwany format gry: {document.GameFormat}");
        }
    }

    public static void ExportWorkspace(BmdDocument document, string directory, Color[] palette, bool useAlpha)
    {
        Directory.CreateDirectory(directory);
        var lines = new List<string>
        {
            $"format,{(document.GameFormat == BmdGameFormat.Cultures1 ? "c1" : "c2")}",
            "index,type,x,y"
        };

        foreach (var frame in document.Frames)
        {
            lines.Add(frame.ToMetadataLine());
            if (frame.IsEmpty)
            {
                continue;
            }

            using var bitmap = frame.ToBitmap(palette, useAlpha);
            bitmap.Save(Path.Combine(directory, $"{frame.Number:D4}.png"));
        }

        File.WriteAllLines(Path.Combine(directory, "metadata.csv"), lines);
    }

    public static BmdDocument ImportWorkspace(string directory, Color[] palette, BmdGameFormat fallbackFormat)
    {
        var metadataPath = Path.Combine(directory, "metadata.csv");
        if (!File.Exists(metadataPath))
        {
            return ImportWorkspaceWithoutMetadata(directory, palette, fallbackFormat);
        }

        var lines = File.ReadAllLines(metadataPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var startIndex = 0;
        var document = new BmdDocument { GameFormat = fallbackFormat };
        if (lines.Count > 0 && lines[0].StartsWith("format,", StringComparison.OrdinalIgnoreCase))
        {
            document.GameFormat = lines[0].Contains("c1", StringComparison.OrdinalIgnoreCase)
                ? BmdGameFormat.Cultures1
                : BmdGameFormat.Cultures2;
            startIndex = 1;
        }

        if (startIndex < lines.Count && lines[startIndex].StartsWith("index,", StringComparison.OrdinalIgnoreCase))
        {
            startIndex++;
        }

        foreach (var line in lines.Skip(startIndex))
        {
            var parts = line.Split(',');
            if (parts.Length < 4)
            {
                throw new InvalidDataException($"Nieprawidlowy wpis metadata.csv: {line}");
            }

            var number = int.Parse(parts[0]);
            var type = (BmdFrameType)int.Parse(parts[1]);
            var offsetX = int.Parse(parts[2]);
            var offsetY = int.Parse(parts[3]);
            var frame = new BmdFrame
            {
                Number = number,
                Type = type,
                OffsetX = offsetX,
                OffsetY = offsetY
            };

            if (type != BmdFrameType.Empty)
            {
                var pngPath = Path.Combine(directory, $"{number:D4}.png");
                using var source = new Bitmap(pngPath);
                using var copy = new Bitmap(source);
                frame.LoadFromBitmap(copy, palette);
            }

            document.Frames.Add(frame);
        }

        document.RenumberFrames();
        return document;
    }

    private static BmdDocument ImportWorkspaceWithoutMetadata(string directory, Color[] palette, BmdGameFormat fallbackFormat)
    {
        var pngFiles = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(path => TryParseFrameNumber(path), Comparer<int>.Default)
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (pngFiles.Count == 0)
        {
            throw new FileNotFoundException("Brakuje pliku metadata.csv oraz plikow PNG do importu.", directory);
        }

        var document = new BmdDocument { GameFormat = fallbackFormat };
        for (var i = 0; i < pngFiles.Count; i++)
        {
            var frame = new BmdFrame
            {
                Number = i,
                Type = GetDefaultImportFrameType(fallbackFormat),
                OffsetX = 0,
                OffsetY = 0
            };

            using var source = new Bitmap(pngFiles[i]);
            using var copy = new Bitmap(source);
            frame.LoadFromBitmap(copy, palette);
            document.Frames.Add(frame);
        }

        document.RenumberFrames();
        return document;
    }

    private static int TryParseFrameNumber(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(name, out var number) ? number : int.MaxValue;
    }

    private static BmdFrameType GetDefaultImportFrameType(BmdGameFormat format)
    {
        return format == BmdGameFormat.Cultures1 ? BmdFrameType.Normal : BmdFrameType.Extended;
    }

    private static BmdDocument LoadCultures2(Stream stream, string path)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveOpen: true);

        var headerMagic = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        var frameCount = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();

        if (headerMagic != C2HeaderMagic)
        {
            throw new InvalidDataException($"Nieobslugiwany naglowek BMD C2: {headerMagic}.");
        }

        var frameInfos = ReadFrameInfosC2(reader);
        var pixelBytes = ReadSectionBytesC2(reader);
        var rows = ReadRowsC2(reader);

        var document = new BmdDocument
        {
            SourcePath = path,
            GameFormat = BmdGameFormat.Cultures2
        };

        for (var index = 0; index < frameCount; index++)
        {
            var info = index < frameInfos.Count ? frameInfos[index] : new RawFrameInfo();
            document.Frames.Add(DecodeFrameC2(index, info, pixelBytes, rows));
        }

        document.RenumberFrames();
        return document;
    }

    private static void SaveCultures2(BmdDocument document, BinaryWriter writer)
    {
        ValidateFrameTypes(document, BmdGameFormat.Cultures2);

        var frameInfos = new List<RawFrameInfo>();
        var rows = new List<RawRow>();
        var pixelBytes = new List<byte>();

        document.RenumberFrames();

        foreach (var frame in document.Frames)
        {
            if (frame.IsEmpty)
            {
                frameInfos.Add(new RawFrameInfo
                {
                    Type = (int)BmdFrameType.Empty,
                    Dx = frame.OffsetX,
                    Dy = frame.OffsetY,
                    Width = 0,
                    Height = 0,
                    RowOffset = rows.Count
                });
                continue;
            }

            var firstRow = rows.Count;
            for (var y = 0; y < frame.Height; y++)
            {
                EncodeRowC2(frame, y, rows, pixelBytes);
            }

            frameInfos.Add(new RawFrameInfo
            {
                Type = (int)frame.Type,
                Dx = frame.OffsetX,
                Dy = frame.OffsetY,
                Width = frame.Width,
                Height = frame.Height,
                RowOffset = firstRow
            });
        }

        writer.Write(C2HeaderMagic);
        writer.Write(0);
        writer.Write(0);
        writer.Write(frameInfos.Count);
        writer.Write(pixelBytes.Count);
        writer.Write(rows.Count);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        WriteSectionC2(writer, frameInfos.Count * 24, sectionWriter =>
        {
            foreach (var frameInfo in frameInfos)
            {
                sectionWriter.Write(frameInfo.Type);
                sectionWriter.Write(frameInfo.Dx);
                sectionWriter.Write(frameInfo.Dy);
                sectionWriter.Write(frameInfo.Width);
                sectionWriter.Write(frameInfo.Height);
                sectionWriter.Write(frameInfo.RowOffset);
            }
        });

        WriteSectionC2(writer, pixelBytes.Count, sectionWriter => sectionWriter.Write(pixelBytes.ToArray()));

        WriteSectionC2(writer, rows.Count * 4, sectionWriter =>
        {
            foreach (var row in rows)
            {
                sectionWriter.Write(row.PixelOffset | (row.Indent << 22));
            }
        });
    }

    private static void ValidateFrameTypes(BmdDocument document, BmdGameFormat format)
    {
        foreach (var frame in document.Frames.Where(frame => !frame.IsEmpty))
        {
            var valid = format switch
            {
                BmdGameFormat.Cultures1 => frame.Type is BmdFrameType.Normal or BmdFrameType.Shadow or BmdFrameType.Build,
                BmdGameFormat.Cultures2 => frame.Type is BmdFrameType.Normal or BmdFrameType.Shadow or BmdFrameType.Extended,
                _ => false
            };

            if (!valid)
            {
                throw new InvalidDataException($"Typ klatki {frame.Type} nie pasuje do formatu {format}.");
            }
        }
    }

    private static List<RawFrameInfo> ReadFrameInfosC2(BinaryReader reader)
    {
        ReadSectionHeaderC2(reader, out var sectionLength);
        var frameInfos = new List<RawFrameInfo>();
        for (var i = 0; i < sectionLength / 24; i++)
        {
            frameInfos.Add(new RawFrameInfo
            {
                Type = reader.ReadInt32(),
                Dx = reader.ReadInt32(),
                Dy = reader.ReadInt32(),
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32(),
                RowOffset = reader.ReadInt32()
            });
        }

        return frameInfos;
    }

    private static byte[] ReadSectionBytesC2(BinaryReader reader)
    {
        ReadSectionHeaderC2(reader, out var sectionLength);
        return reader.ReadBytes(sectionLength);
    }

    private static List<RawRow> ReadRowsC2(BinaryReader reader)
    {
        ReadSectionHeaderC2(reader, out var sectionLength);
        return ReadRowsCore(reader, sectionLength);
    }

    private static List<RawRow> ReadRowsCore(BinaryReader reader, int sectionLength)
    {
        var rows = new List<RawRow>();
        for (var i = 0; i < sectionLength / 4; i++)
        {
            var value = reader.ReadInt32();
            rows.Add(new RawRow
            {
                PixelOffset = value & EmptyRowOffset,
                Indent = (value >> 22) & EmptyRowIndent
            });
        }

        return rows;
    }

    private static void ReadSectionHeaderC2(BinaryReader reader, out int sectionLength)
    {
        var magic = reader.ReadInt32();
        _ = reader.ReadInt32();
        sectionLength = reader.ReadInt32();
        if (magic != C2SectionMagic)
        {
            throw new InvalidDataException($"Nieobslugiwany naglowek sekcji BMD C2: {magic}.");
        }
    }

    private static BmdFrame DecodeFrameC2(int index, RawFrameInfo info, IReadOnlyList<byte> pixelBytes, IReadOnlyList<RawRow> rows)
    {
        var frame = new BmdFrame
        {
            Number = index,
            OffsetX = info.Dx,
            OffsetY = info.Dy,
            Type = Enum.IsDefined(typeof(BmdFrameType), info.Type) ? (BmdFrameType)info.Type : BmdFrameType.Empty
        };

        if (frame.Type == BmdFrameType.Empty || info.Width <= 0 || info.Height <= 0)
        {
            frame.Type = BmdFrameType.Empty;
            frame.Pixels = null;
            return frame;
        }

        frame.Pixels = new IndexedPixel[info.Height][];
        for (var y = 0; y < info.Height; y++)
        {
            frame.Pixels[y] = Enumerable.Range(0, info.Width).Select(_ => new IndexedPixel()).ToArray();
            var row = rows[info.RowOffset + y];
            if (row.PixelOffset == EmptyRowOffset && row.Indent == EmptyRowIndent)
            {
                continue;
            }

            var pointer = row.PixelOffset;
            var x = row.Indent;
            while (pointer < pixelBytes.Count)
            {
                var blockLength = pixelBytes[pointer++];
                if (blockLength == 0)
                {
                    break;
                }

                if (blockLength < 0x80)
                {
                    for (var i = 0; i < blockLength && x < info.Width; i++)
                    {
                        frame.Pixels[y][x++] = frame.Type switch
                        {
                            BmdFrameType.Normal => new IndexedPixel
                            {
                                PaletteIndex = pixelBytes[pointer++],
                                Alpha = 255
                            },
                            BmdFrameType.Shadow => new IndexedPixel
                            {
                                PaletteIndex = 0,
                                Alpha = 128
                            },
                            BmdFrameType.Extended => new IndexedPixel
                            {
                                PaletteIndex = pixelBytes[pointer++],
                                Alpha = pixelBytes[pointer++]
                            },
                            _ => new IndexedPixel()
                        };
                    }
                }
                else
                {
                    x += blockLength - 0x80;
                }
            }
        }

        return frame;
    }

    private static void EncodeRowC2(BmdFrame frame, int rowIndex, ICollection<RawRow> rows, ICollection<byte> pixelBytes)
    {
        var rowPixels = frame.Pixels![rowIndex];
        var firstVisible = Array.FindIndex(rowPixels, pixel => pixel.Alpha > 0);
        if (firstVisible < 0)
        {
            rows.Add(new RawRow { PixelOffset = EmptyRowOffset, Indent = EmptyRowIndent });
            return;
        }

        var rowBytes = new List<byte>();
        var x = firstVisible;
        while (x < rowPixels.Length)
        {
            if (rowPixels[x].Alpha == 0)
            {
                var transparentLength = 0;
                while (x < rowPixels.Length && rowPixels[x].Alpha == 0 && transparentLength < 127)
                {
                    transparentLength++;
                    x++;
                }

                rowBytes.Add((byte)(0x80 + transparentLength));
                continue;
            }

            var visibleStart = x;
            var visibleLength = 0;
            while (x < rowPixels.Length && rowPixels[x].Alpha > 0 && visibleLength < 127)
            {
                visibleLength++;
                x++;
            }

            rowBytes.Add((byte)visibleLength);
            for (var i = 0; i < visibleLength; i++)
            {
                var pixel = rowPixels[visibleStart + i];
                switch (frame.Type)
                {
                    case BmdFrameType.Normal:
                        rowBytes.Add(pixel.PaletteIndex);
                        break;
                    case BmdFrameType.Shadow:
                        break;
                    case BmdFrameType.Extended:
                        rowBytes.Add(pixel.PaletteIndex);
                        rowBytes.Add(pixel.Alpha);
                        break;
                }
            }
        }

        rowBytes.Add(0);
        rows.Add(new RawRow { PixelOffset = pixelBytes.Count, Indent = firstVisible });
        foreach (var value in rowBytes)
        {
            pixelBytes.Add(value);
        }
    }

    private static void WriteSectionC2(BinaryWriter writer, int sectionLength, Action<BinaryWriter> writeBody)
    {
        writer.Write(C2SectionMagic);
        writer.Write(0);
        writer.Write(sectionLength);
        writeBody(writer);
    }


    private sealed class RawFrameInfo
    {
        public int Type { get; set; }
        public int Dx { get; set; }
        public int Dy { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RowOffset { get; set; }
    }

    private sealed class RawRow
    {
        public int PixelOffset { get; set; }
        public int Indent { get; set; }
    }
}
