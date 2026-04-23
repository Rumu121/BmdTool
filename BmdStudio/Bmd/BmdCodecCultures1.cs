namespace BMDEditor.Bmd;

internal static class BmdCodecCultures1
{
    private const int HeaderMagic = 25;
    private const int SectionMagic = 10;
    private const int EmptyRowOffset = 0x3FFFFF;
    private const int EmptyRowIndent = 0x3FF;

    public static BmdDocument Load(Stream stream, string path)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveOpen: true);

        if (reader.ReadInt32() != HeaderMagic)
        {
            throw new InvalidDataException("Niepoprawny naglowek BMD C1.");
        }

        var frameIndexOffset = reader.ReadInt32();
        var frameCount = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();

        var frameInfos = ReadFrameInfos(reader);
        var pixelBytes = ReadSectionBytes(reader);
        var rows = ReadRows(reader);

        var document = new BmdDocument
        {
            SourcePath = path,
            GameFormat = BmdGameFormat.Cultures1
        };

        for (var index = 0; index < frameIndexOffset; index++)
        {
            document.Frames.Add(new BmdFrame
            {
                Number = index,
                Type = BmdFrameType.Empty
            });
        }

        for (var index = 0; index < frameCount; index++)
        {
            var info = index < frameInfos.Count ? frameInfos[index] : new RawFrameInfo();
            document.Frames.Add(DecodeFrame(frameIndexOffset + index, info, pixelBytes, rows));
        }

        document.RenumberFrames();
        return document;
    }

    public static void Save(BmdDocument document, BinaryWriter writer)
    {
        ValidateFrameTypes(document);

        var numberOfFrames = document.Frames.Count == 0 ? 0 : document.Frames.Max(frame => frame.Number) + 1;
        var numberOfRows = 0;
        var frameIndexOffset = 0;
        var insertOffset = true;

        var frameSection = new List<byte>();
        var pixelSection = new List<byte>();
        var rowSection = new List<byte>();

        for (var frameIndex = 0; frameIndex < numberOfFrames; frameIndex++)
        {
            var frame = document.Frames.FirstOrDefault(candidate => candidate.Number == frameIndex);
            if (frame is null || frame.IsEmpty)
            {
                if (insertOffset)
                {
                    frameIndexOffset++;
                }
                else
                {
                    WriteFrameInfo(frameSection, new RawFrameInfo());
                }

                continue;
            }

            insertOffset = false;

            var rowOffset = rowSection.Count / 4;
            for (var rowIndex = 0; rowIndex < frame.Height; rowIndex++)
            {
                EncodeRow(frame, rowIndex, rowSection, pixelSection);
            }

            numberOfRows += frame.Height;
            WriteFrameInfo(frameSection, new RawFrameInfo
            {
                Type = frame.Type switch
                {
                    BmdFrameType.Normal => 1,
                    BmdFrameType.Shadow => 2,
                    BmdFrameType.Build => 3,
                    _ => 0
                },
                Dx = frame.OffsetX,
                Dy = frame.OffsetY,
                Width = frame.Width,
                Height = frame.Height,
                RowOffset = rowOffset
            });
        }

        writer.Write(HeaderMagic);
        writer.Write(frameIndexOffset);
        writer.Write(numberOfFrames - frameIndexOffset);
        writer.Write(CountPixels(frameSection, pixelSection, rowSection));
        writer.Write(numberOfRows);
        writer.Write(numberOfRows);
        writer.Write(0);
        writer.Write(0);

        if (numberOfFrames != 0)
        {
            WriteSection(writer, frameSection);
            WriteSection(writer, pixelSection);
            WriteSection(writer, rowSection);
        }
    }

    private static void ValidateFrameTypes(BmdDocument document)
    {
        foreach (var frame in document.Frames.Where(frame => !frame.IsEmpty))
        {
            if (frame.Type is not (BmdFrameType.Normal or BmdFrameType.Shadow or BmdFrameType.Build))
            {
                throw new InvalidDataException($"Typ klatki {frame.Type} nie jest obslugiwany dla Cultures 1.");
            }
        }
    }

    private static List<RawFrameInfo> ReadFrameInfos(BinaryReader reader)
    {
        ReadSectionHeader(reader, out var sectionLength);
        var frameInfos = new List<RawFrameInfo>();
        for (var i = 0; i < sectionLength / 28; i++)
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
            _ = reader.ReadInt32();
        }

        return frameInfos;
    }

    private static byte[] ReadSectionBytes(BinaryReader reader)
    {
        ReadSectionHeader(reader, out var sectionLength);
        return reader.ReadBytes(sectionLength);
    }

    private static List<RawRow> ReadRows(BinaryReader reader)
    {
        ReadSectionHeader(reader, out var sectionLength);
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

    private static void ReadSectionHeader(BinaryReader reader, out int sectionLength)
    {
        var magic = reader.ReadInt32();
        sectionLength = reader.ReadInt32();
        if (magic != SectionMagic)
        {
            throw new InvalidDataException($"Nieobslugiwany naglowek sekcji BMD C1: {magic}.");
        }
    }

    private static BmdFrame DecodeFrame(int index, RawFrameInfo info, IReadOnlyList<byte> pixelBytes, IReadOnlyList<RawRow> rows)
    {
        var frame = new BmdFrame
        {
            Number = index,
            OffsetX = info.Dx,
            OffsetY = info.Dy,
            Type = info.Type switch
            {
                1 => BmdFrameType.Normal,
                2 => BmdFrameType.Shadow,
                3 => BmdFrameType.Build,
                _ => BmdFrameType.Empty
            }
        };

        if (frame.Type == BmdFrameType.Empty || info.Width <= 0 || info.Height <= 0)
        {
            frame.Pixels = null;
            return frame;
        }

        frame.Pixels = new IndexedPixel[info.Height][];

        for (var rowIndex = 0; rowIndex < info.Height; rowIndex++)
        {
            var row = Enumerable.Range(0, info.Width).Select(_ => new IndexedPixel()).ToArray();
            var rowHeader = rows[info.RowOffset + rowIndex];

            if (rowHeader.Indent == EmptyRowIndent)
            {
                frame.Pixels[rowIndex] = row;
                continue;
            }

            var x = rowHeader.Indent;
            var pointer = rowHeader.PixelOffset;
            var head = -1;

            while (head != 0)
            {
                head = frame.Type == BmdFrameType.Build
                    ? pixelBytes[pointer++]
                    : unchecked((sbyte)pixelBytes[pointer++]);

                if (head > 0)
                {
                    switch (frame.Type)
                    {
                        case BmdFrameType.Normal:
                            for (var i = 0; i < head && x < info.Width; i++)
                            {
                                row[x++] = new IndexedPixel
                                {
                                    PaletteIndex = pixelBytes[pointer++],
                                    Alpha = 255
                                };
                            }
                            break;
                        case BmdFrameType.Shadow:
                            for (var i = 0; i < head && x < info.Width; i++)
                            {
                                row[x++] = new IndexedPixel
                                {
                                    PaletteIndex = 0,
                                    Alpha = 255
                                };
                            }
                            break;
                        case BmdFrameType.Build:
                            var value = pixelBytes[pointer++];
                            if (value == 255)
                            {
                                x += head;
                            }
                            else
                            {
                                for (var i = 0; i < head && x < info.Width; i++)
                                {
                                    row[x++] = new IndexedPixel
                                    {
                                        PaletteIndex = value,
                                        Alpha = 255
                                    };
                                }
                            }
                            break;
                    }
                }
                else if (head < 0)
                {
                    x += head + 128;
                }
                else
                {
                    x = info.Width;
                }
            }

            frame.Pixels[rowIndex] = row;
        }

        return frame;
    }

    private static void EncodeRow(BmdFrame frame, int rowIndex, List<byte> rowSection, List<byte> pixelSection)
    {
        var row = frame.Pixels![rowIndex];
        var encoded = new List<int>();

        if (frame.Type == BmdFrameType.Build)
        {
            EncodeBuildRow(row, encoded);
            var indent = 0;
            if (encoded.Count == 0)
            {
                indent = -1;
            }
            else if (encoded.Count >= 2 && encoded[1] == 255)
            {
                indent = encoded[0];
                encoded.RemoveRange(0, 2);
            }

            WriteRowHeader(rowSection, indent, pixelSection.Count);
            foreach (var value in encoded)
            {
                pixelSection.Add((byte)value);
            }

            if (indent != -1)
            {
                pixelSection.Add(0);
            }

            return;
        }

        var currentAlpha = true;
        var pixelsCount = 0;
        var rowToAdd = new List<int>();

        foreach (var pixel in row)
        {
            var color = pixel.Alpha != 0 ? pixel.PaletteIndex : -1;
            if (frame.Type == BmdFrameType.Shadow)
            {
                color = pixel.Alpha == 0 ? -1 : 0;
            }

            if (color == -1)
            {
                if (!currentAlpha && pixelsCount > 0)
                {
                    InsertPixels(encoded, frame.Type, rowToAdd, pixelsCount, 0);
                    rowToAdd.Clear();
                    pixelsCount = 0;
                }

                pixelsCount++;
                if (pixelsCount == 127)
                {
                    encoded.Add(-1);
                    pixelsCount = 0;
                }

                currentAlpha = true;
            }
            else
            {
                if (currentAlpha && pixelsCount > 0)
                {
                    encoded.Add((pixelsCount - 128) % 256);
                    pixelsCount = 0;
                }

                rowToAdd.Add(color);
                pixelsCount++;
                if (pixelsCount == 127)
                {
                    InsertPixels(encoded, frame.Type, rowToAdd, pixelsCount, 0);
                    rowToAdd.Clear();
                    pixelsCount = 0;
                }

                currentAlpha = false;
            }
        }

        if (pixelsCount > 0 && !currentAlpha)
        {
            InsertPixels(encoded, frame.Type, rowToAdd, pixelsCount, 0);
        }

        var indentValue = 0;
        if (encoded.Count == 0)
        {
            indentValue = -1;
        }
        else if (encoded[0] == -1)
        {
            encoded.RemoveAt(0);
            indentValue = 127;
        }
        else if (encoded[0] > 128)
        {
            indentValue = encoded[0] - 128;
            encoded.RemoveAt(0);
        }

        while (encoded.Count > 0 && encoded[^1] == -1)
        {
            encoded.RemoveAt(encoded.Count - 1);
        }

        for (var i = 0; i < encoded.Count; i++)
        {
            if (encoded[i] == -1)
            {
                encoded[i] = 255;
            }
        }

        WriteRowHeader(rowSection, indentValue == 0 && encoded.Count == 0 ? -1 : indentValue, pixelSection.Count);
        if (indentValue != -1 || encoded.Count > 0)
        {
            foreach (var value in encoded)
            {
                pixelSection.Add((byte)value);
            }

            pixelSection.Add(0);
        }
    }

    private static void EncodeBuildRow(IReadOnlyList<IndexedPixel> row, ICollection<int> encoded)
    {
        var currentColor = -1;
        var pixelsCount = 0;

        foreach (var pixel in row)
        {
            var color = pixel.Alpha != 0 ? pixel.PaletteIndex : 255;
            if (pixelsCount == 0)
            {
                currentColor = color;
                pixelsCount = 1;
            }
            else if (color != currentColor || pixelsCount >= 255)
            {
                encoded.Add(pixelsCount);
                encoded.Add(currentColor);
                currentColor = color;
                pixelsCount = 1;
            }
            else
            {
                pixelsCount++;
            }
        }

        if (pixelsCount > 0)
        {
            encoded.Add(pixelsCount);
            encoded.Add(currentColor);
        }

        while (encoded.Count >= 2)
        {
            var values = encoded.ToList();
            if (values[^2] != 1 || values[^1] != 255)
            {
                break;
            }

            values.RemoveRange(values.Count - 2, 2);
            encoded.Clear();
            foreach (var value in values)
            {
                encoded.Add(value);
            }
        }
    }

    private static void InsertPixels(ICollection<int> encoded, BmdFrameType type, IReadOnlyCollection<int> rowToAdd, int pixelsCount, int currentColor)
    {
        switch (type)
        {
            case BmdFrameType.Normal:
                encoded.Add(rowToAdd.Count);
                foreach (var value in rowToAdd)
                {
                    encoded.Add(value);
                }
                break;
            case BmdFrameType.Shadow:
                encoded.Add(rowToAdd.Count);
                break;
            case BmdFrameType.Build:
                encoded.Add(pixelsCount);
                encoded.Add(currentColor);
                break;
        }
    }

    private static void WriteRowHeader(List<byte> rowSection, int indent, int pixelOffset)
    {
        if (indent == -1)
        {
            WriteInt32(rowSection, -1);
            return;
        }

        WriteInt32(rowSection, pixelOffset | (indent << 22));
    }

    private static void WriteFrameInfo(List<byte> frameSection, RawFrameInfo info)
    {
        WriteInt32(frameSection, info.Type);
        WriteInt32(frameSection, info.Dx);
        WriteInt32(frameSection, info.Dy);
        WriteInt32(frameSection, info.Width);
        WriteInt32(frameSection, info.Height);
        WriteInt32(frameSection, info.RowOffset);
        WriteInt32(frameSection, 0);
    }

    private static int CountPixels(IReadOnlyList<byte> frameSection, IReadOnlyList<byte> pixelSection, IReadOnlyList<byte> rowSection)
    {
        var count = 0;
        for (var frameIndex = 0; frameIndex < frameSection.Count / 28; frameIndex++)
        {
            var frameOffset = frameIndex * 28;
            var frameType = BitConverter.ToInt32(frameSection.Skip(frameOffset).Take(4).ToArray(), 0);
            var height = BitConverter.ToInt32(frameSection.Skip(frameOffset + 16).Take(4).ToArray(), 0);
            var rowOffset = BitConverter.ToInt32(frameSection.Skip(frameOffset + 20).Take(4).ToArray(), 0);

            for (var rowIndex = 0; rowIndex < height; rowIndex++)
            {
                var rowValue = BitConverter.ToInt32(rowSection.Skip((rowOffset + rowIndex) * 4).Take(4).ToArray(), 0);
                var indent = (rowValue >> 22) & EmptyRowIndent;
                if (indent == EmptyRowIndent)
                {
                    continue;
                }

                var pointer = rowValue & EmptyRowOffset;
                var head = -1;
                while (head != 0)
                {
                    head = frameType == 3
                        ? pixelSection[pointer++]
                        : unchecked((sbyte)pixelSection[pointer++]);
                    count++;

                    if (head > 0)
                    {
                        switch (frameType)
                        {
                            case 1:
                                pointer += head;
                                count += head;
                                break;
                            case 3:
                                pointer++;
                                count++;
                                break;
                        }
                    }
                }
            }
        }

        return count;
    }

    private static void WriteSection(BinaryWriter writer, IReadOnlyCollection<byte> section)
    {
        writer.Write(SectionMagic);
        writer.Write(section.Count);
        writer.Write(section.ToArray());
    }

    private static void WriteInt32(ICollection<byte> buffer, int value)
    {
        foreach (var b in BitConverter.GetBytes(value))
        {
            buffer.Add(b);
        }
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
