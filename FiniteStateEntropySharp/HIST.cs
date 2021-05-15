using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace FiniteStateEntropySharp
{
    internal static class HIST
    {
        internal const int HIST_WKSP_SIZE_U32 = 1024;
        internal const int HIST_WKSP_SIZE = HIST_WKSP_SIZE_U32 * sizeof(uint);

        internal unsafe static int hist_count(uint[] count, ref uint maxSymbolValue, ReadOnlySpan<byte> source, int sourceSize)
        {
            uint[] temp_counters = new uint[HIST_WKSP_SIZE];

            fixed (uint* temp_counters_ptr = temp_counters)
            {
                return hist_count_wksp(count, ref maxSymbolValue, source, sourceSize, temp_counters_ptr, HIST_WKSP_SIZE);
            }
        }
        internal unsafe static int hist_count_wksp(uint[] count, ref uint maxSymbolValue, ReadOnlySpan<byte> source, int sourceSize, uint* workspace, int workspaceSize)
        {
            if (((int)workspace & 3) > 0) throw new ArgumentException(nameof(workspace));
            if (workspaceSize < HIST_WKSP_SIZE) throw new ArgumentOutOfRangeException(nameof(workspaceSize));

            if (maxSymbolValue < 255)
            {
                return hist_count_parallel_wksp(count, ref maxSymbolValue, source, sourceSize, hist_check_input.check_max_symbol_value, workspace);
            }
            else
            {
                maxSymbolValue = 255;
                return hist_count_fast_wksp(count, ref maxSymbolValue, source, sourceSize, workspace, workspaceSize);
            }

        }

        internal unsafe static int hist_count_fast(uint[] count, ref uint maxSymbolValue, ReadOnlySpan<byte> source, int sourceSize)
        {
            uint[] temp_counters = new uint[HIST_WKSP_SIZE];

            fixed (uint* temp_counters_ptr = temp_counters)
            {
                return hist_count_fast_wksp(count, ref maxSymbolValue, source, sourceSize, temp_counters_ptr, HIST_WKSP_SIZE);
            }
        }
        internal unsafe static int hist_count_fast_wksp(uint[] count, ref uint maxSymbolValue, ReadOnlySpan<byte> source, int sourceSize, uint* workspace, int workspaceSize)
        {
            if (sourceSize < 1500)
            {
                return (int)hist_count_simple(count, ref maxSymbolValue, source, sourceSize);
            }
            else
            {
                if (((int)workspace & 3) > 0) throw new ArgumentException(nameof(workspace));
                if (workspaceSize < HIST_WKSP_SIZE) throw new ArgumentOutOfRangeException(nameof(workspaceSize));

                return hist_count_parallel_wksp(count, ref maxSymbolValue, source, sourceSize, hist_check_input.trust_input, workspace);
            }
        }

        private unsafe static uint hist_count_simple(uint[] count, ref uint maxSymbolValue, ReadOnlySpan<byte> source, int sourceSize)
        {
            uint maxSymbolValueLocal = maxSymbolValue;
            uint largestCount = 0;

            int src_i = 0;
            while (src_i < sourceSize)
            {
                count[source[src_i++]]++;
            }

            while (count[maxSymbolValueLocal] == 0) maxSymbolValueLocal--;
            maxSymbolValue = maxSymbolValueLocal;

            for (int s = 0; s <= maxSymbolValueLocal; s++)
            {
                if (count[s] > largestCount) largestCount = count[s];
            }

            return largestCount;
        }

        private static unsafe int hist_count_parallel_wksp(uint[] count, ref uint maxSymbolValue, ReadOnlySpan<byte> source, int sourceSize, hist_check_input checkInput, uint* workspace)
        {
            int source_offset = 0;
            int count_size = ((int)maxSymbolValue + 1) * sizeof(uint);
            uint max = 0;

            uint* counting1 = workspace;
            uint* counting2 = counting1 + 256;
            uint* counting3 = counting2 + 256;
            uint* counting4 = counting3 + 256;

            if (sourceSize == 0)
            {
                maxSymbolValue = 0;
                return 0;
            }

            uint cached = BinaryPrimitives.ReadUInt32LittleEndian(source[source_offset..]); source_offset += 4;
            while (source_offset < sourceSize - 15)
            {
                uint c = cached; cached = BinaryPrimitives.ReadUInt32LittleEndian(source[source_offset..]); source_offset += 4;
                counting1[(byte)(c >> 00)]++;
                counting2[(byte)(c >> 08)]++;
                counting3[(byte)(c >> 16)]++;
                counting4[c >> 24]++;

                c = cached; cached = BinaryPrimitives.ReadUInt32LittleEndian(source[source_offset..]); source_offset += 4;
                counting1[(byte)(c >> 00)]++;
                counting2[(byte)(c >> 08)]++;
                counting3[(byte)(c >> 16)]++;
                counting4[c >> 24]++;

                c = cached; cached = BinaryPrimitives.ReadUInt32LittleEndian(source[source_offset..]); source_offset += 4;
                counting1[(byte)(c >> 00)]++;
                counting2[(byte)(c >> 08)]++;
                counting3[(byte)(c >> 16)]++;
                counting4[c >> 24]++;

                c = cached; cached = BinaryPrimitives.ReadUInt32LittleEndian(source[source_offset..]); source_offset += 4;
                counting1[(byte)(c >> 00)]++;
                counting2[(byte)(c >> 08)]++;
                counting3[(byte)(c >> 16)]++;
                counting4[c >> 24]++;
            }

            source_offset -= 4;

            while (source_offset < sourceSize) counting1[source[source_offset++]]++;

            for (uint s = 0; s < 256; s++)
            {
                counting1[s] += counting2[s] + counting3[s] + counting4[s];
                if (counting1[s] > max) max = counting1[s];
            }

            uint maxSymbolValueLocal = 255;
            while (counting1[maxSymbolValue] == 0) maxSymbolValue--;

            if(checkInput == hist_check_input.check_max_symbol_value 
                && maxSymbolValueLocal > maxSymbolValue)
            {
                throw new InvalidOperationException("MaxSymbolValue too small");
            }

            maxSymbolValue = maxSymbolValueLocal;
        
            for(int i = 0; i < count_size; i++)
            {
                count[i] = counting1[i];
            }

            return (int)max;
        }


        private enum hist_check_input
        {
            trust_input,
            check_max_symbol_value
        }
    }
}
