using System.Buffers.Binary;

namespace GameLanguageAssistant.Audio;

public static class AudioLevel
{
    public static float MeasurePeak(ReadOnlySpan<byte> buffer, int bits, bool floatingPoint)
    {
        if (floatingPoint ? bits != 32 : bits is not (16 or 24 or 32))
            throw new NotSupportedException("지원하지 않는 오디오 형식입니다.");

        var bytes = bits / 8;
        float peak = 0;
        for (var offset = 0; offset + bytes <= buffer.Length; offset += bytes)
        {
            var sample = buffer.Slice(offset, bytes);
            float value;
            if (floatingPoint)
                value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample));
            else if (bits == 16)
                value = BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f;
            else if (bits == 24)
            {
                var signed = (sample[0] | sample[1] << 8 | sample[2] << 16) << 8 >> 8;
                value = signed / 8388608f;
            }
            else
                value = BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648f;

            if (float.IsFinite(value)) peak = Math.Max(peak, Math.Abs(value));
        }
        return Math.Clamp(peak, 0, 1);
    }
}
