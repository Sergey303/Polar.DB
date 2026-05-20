using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Polar.DB
{
    /// <summary>
    /// Sequence storage format: [Int64 count][serialized payload...].
    /// fs.Position is an internal working cursor and is not restored by hot-path methods.
    /// AppendOffset is the authoritative logical tail.
    /// </summary>
    public class UniversalSequenceBase
    {
        private const long HeaderSize = 8L;

        protected PType tp_elem;
        protected Stream fs;
        internal Stream Media { get { return fs; } }
        public long AppendOffset => append_offset;

        private readonly BinaryReader br;
        private readonly BinaryWriter bw;
        protected int elem_size = -1;
        private long nelements;
        private long append_offset = HeaderSize;

        public UniversalSequenceBase(PType tp_el, Stream media)
        {
            tp_elem = tp_el ?? throw new ArgumentNullException(nameof(tp_el));
            fs = media ?? throw new ArgumentNullException(nameof(media));
            if (tp_elem.HasNoTail) elem_size = tp_elem.HeadSize;
            br = new BinaryReader(fs);
            bw = new BinaryWriter(fs);

            if (fs.Length == 0)
            {
                Clear();
            }
            else
            {
                RecoverFromExistingStream(rewriteHeader: true, strict: false);
            }
        }

        public void Clear()
        {
            fs.Position = 0L;
            fs.SetLength(0L);
            bw.Write(0L);
            nelements = 0L;
            append_offset = HeaderSize;
            fs.Position = append_offset;
            fs.Flush();
        }

        public void Flush()
        {
            fs.Position = 0L;
            bw.Write(nelements);
            fs.Position = append_offset;
            fs.Flush();
        }

        public void Close()
        {
            Flush();
            fs.Close();
        }

        public void Refresh()
        {
            if (fs.Length == 0L)
            {
                Clear();
                return;
            }

            RecoverFromExistingStream(rewriteHeader: true, strict: true);
        }

        public long Count() { return nelements; }

        public long ElementOffset(long ind)
        {
            if (elem_size <= 0)
                throw new InvalidOperationException("ElementOffset(index) is available only for fixed-size elements.");
            if (ind < 0 || ind >= nelements)
                throw new ArgumentOutOfRangeException(nameof(ind));

            return HeaderSize + ind * elem_size;
        }

        public long ElementOffset() { return append_offset; }

        public long SetElement(object v)
        {
            long pos = fs.Position;
            ByteFlow.Serialize(bw, v, tp_elem);
            if (pos == append_offset)
                append_offset = fs.Position;
            return pos;
        }

        public void SetElement(object v, long off)
        {
            SetTypedElementCore(tp_elem, v, off);
        }

        public void SetTypedElement(PType tp, object v, long off)
        {
            if (tp == null) throw new ArgumentNullException(nameof(tp));
            SetTypedElementCore(tp, v, off);
        }

        public long AppendElement(object v)
        {
            _ = v ?? throw new ArgumentNullException(nameof(v));

            long off = append_offset;
            fs.Position = off;
            ByteFlow.Serialize(bw, v, tp_elem);
            append_offset = fs.Position;
            nelements += 1L;
            return off;
        }

        public object GetElement()
        {
            return ByteFlow.Deserialize(br, tp_elem);
        }

        public object GetElement(long off)
        {
            ValidateReadOffset(off);
            fs.Position = off;
            return GetElement();
        }

        public object GetTypedElement(PType tp, long off)
        {
            if (tp == null) throw new ArgumentNullException(nameof(tp));
            ValidateReadOffset(off);
            fs.Position = off;
            return ByteFlow.Deserialize(br, tp);
        }

        public object GetByIndex(long index)
        {
            if (elem_size <= 0)
                throw new InvalidOperationException("GetByIndex is available only for fixed-size elements.");
            if (index < 0 || index >= nelements)
                throw new IndexOutOfRangeException();

            return GetElement(ElementOffset(index));
        }

        public IEnumerable<object> ElementValues()
        {
            fs.Position = HeaderSize;
            for (long i = 0; i < Count(); i++)
            {
                yield return GetElement();
            }
        }

        public IEnumerable<object> ElementValues(long offset, long number)
        {
            ValidateRange(offset, number);
            fs.Position = offset;
            for (long i = 0; i < number; i++)
            {
                yield return GetElement();
            }
        }

        public void Scan(Func<long, object, bool> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (nelements == 0L) return;

            fs.Position = HeaderSize;
            for (long ii = 0; ii < nelements; ii++)
            {
                long off = fs.Position;
                object pobject = GetElement();
                if (!handler(off, pobject)) break;
            }
        }

        public IEnumerable<Tuple<long, object>> ElementOffsetValuePairs()
        {
            fs.Position = HeaderSize;
            for (long i = 0; i < Count(); i++)
            {
                long off = fs.Position;
                object pobject = GetElement();
                yield return new Tuple<long, object>(off, pobject);
            }
        }

        public IEnumerable<Tuple<long, object>> ElementOffsetValuePairs(long offset, long number)
        {
            ValidateRange(offset, number);
            fs.Position = offset;
            for (long i = 0; i < number; i++)
            {
                long off = fs.Position;
                object pobject = GetElement();
                yield return new Tuple<long, object>(off, pobject);
            }
        }

        public void Sort32(Func<object, int> keyFun)
        {
            if (keyFun == null) throw new ArgumentNullException(nameof(keyFun));
            if (!tp_elem.HasNoTail) throw new InvalidOperationException("Sort32 is available only for fixed-size elements.");
            S32(0, Count(), keyFun);
        }

        private void S32(long start, long numb, Func<object, int> keyFun)
        {
            int[] keys = new int[numb];
            object[] records = new object[numb];
            long pos = start;
            Scan((off, obj) =>
            {
                keys[pos] = keyFun(obj);
                records[pos] = obj;
                pos++;
                return true;
            });
            Array.Sort(keys, records);
            Clear();
            for (long ii = 0; ii < keys.LongLength; ii++)
            {
                AppendElement(records[ii]);
            }
            Flush();
        }

        public void Sort64(Func<object, long> keyFun)
        {
            if (keyFun == null) throw new ArgumentNullException(nameof(keyFun));
            if (!tp_elem.HasNoTail) throw new InvalidOperationException("Sort64 is available only for fixed-size elements.");
            S64(0, Count(), keyFun);
        }

        private void S64(long start, long numb, Func<object, long> keyFun)
        {
            long[] keys = new long[numb];
            object[] records = new object[numb];
            long pos = start;
            Scan((off, obj) =>
            {
                keys[pos] = keyFun(obj);
                records[pos] = obj;
                pos++;
                return true;
            });
            Array.Sort(keys, records);
            Clear();
            for (long ii = 0; ii < keys.LongLength; ii++)
            {
                AppendElement(records[ii]);
            }
            Flush();
        }

        private void SetTypedElementCore(PType tp, object v, long off)
        {
            if (off < HeaderSize || off > append_offset)
                throw new ArgumentOutOfRangeException(nameof(off));

            long originalAppendOffset = append_offset;
            long originalLength = fs.Length;
            long originalCount = nelements;
            byte[]? originalBytes = null;

            if (off < originalAppendOffset)
            {
                long bytesToSave = originalAppendOffset - off;
                if (bytesToSave > int.MaxValue)
                    throw new InvalidOperationException("Rollback buffer is too large.");

                originalBytes = new byte[bytesToSave];
                fs.Position = off;
                int read = fs.Read(originalBytes, 0, originalBytes.Length);
                if (read != originalBytes.Length)
                    throw new InvalidDataException("Cannot snapshot existing element bytes for rollback.");
            }

            try
            {
                fs.Position = off;
                ByteFlow.Serialize(bw, v, tp);

                if (off == originalAppendOffset)
                {
                    append_offset = fs.Position;
                }
                else if (fs.Position > originalAppendOffset)
                {
                    throw new InvalidOperationException("SetElement crossed the logical end of sequence.");
                }
            }
            catch
            {
                if (originalBytes != null)
                {
                    fs.Position = off;
                    fs.Write(originalBytes, 0, originalBytes.Length);
                }
                fs.SetLength(originalLength);
                append_offset = originalAppendOffset;
                nelements = originalCount;
                fs.Position = Math.Min(append_offset, fs.Length);
                fs.Flush();
                throw;
            }
        }

        private void ValidateReadOffset(long off)
        {
            if (off < HeaderSize || off >= append_offset)
                throw new ArgumentOutOfRangeException(nameof(off));
        }

        private void ValidateRange(long offset, long number)
        {
            if (offset < HeaderSize || offset > append_offset)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (number < 0)
                throw new ArgumentOutOfRangeException(nameof(number));

            if (elem_size > 0)
            {
                checked
                {
                    long end = offset + number * elem_size;
                    if (end > append_offset)
                        throw new ArgumentOutOfRangeException(nameof(number));
                }
            }
        }

        private void RecoverFromExistingStream(bool rewriteHeader, bool strict)
        {
            if (fs.Length < HeaderSize)
                throw new InvalidDataException("UniversalSequenceBase header is truncated.");

            fs.Position = 0L;
            long declaredCount = br.ReadInt64();
            if (declaredCount < 0)
                throw new InvalidDataException("UniversalSequenceBase declared count is negative.");

            if (elem_size > 0)
            {
                RecoverFixedSize(declaredCount, rewriteHeader, strict);
            }
            else
            {
                RecoverVariableSize(declaredCount, rewriteHeader, strict);
            }
        }

        private void RecoverFixedSize(long declaredCount, bool rewriteHeader, bool strict)
        {
            long payloadBytes = fs.Length - HeaderSize;
            if (payloadBytes < 0)
                throw new InvalidDataException("UniversalSequenceBase payload is truncated.");

            if (payloadBytes % elem_size != 0)
            {
                if (strict)
                    throw new InvalidDataException("UniversalSequenceBase fixed-size payload length does not match declared count.");
            }

            long physicalCount = payloadBytes / elem_size;
            if (strict && physicalCount != declaredCount)
                throw new InvalidDataException("UniversalSequenceBase fixed-size payload length does not match declared count.");

            nelements = strict ? declaredCount : Math.Min(declaredCount, physicalCount);
            append_offset = HeaderSize + nelements * elem_size;

            if (fs.Length != append_offset)
                fs.SetLength(append_offset);

            if (rewriteHeader)
                Flush();
            else
                fs.Position = append_offset;
        }

        private void RecoverVariableSize(long declaredCount, bool rewriteHeader, bool strict)
        {
            fs.Position = HeaderSize;
            long count = 0L;
            long logicalEnd = HeaderSize;

            while (count < declaredCount)
            {
                long off = fs.Position;
                if (off >= fs.Length)
                {
                    if (strict)
                        throw new InvalidDataException("UniversalSequenceBase variable-size payload is truncated.");
                    break;
                }

                try
                {
                    _ = ByteFlow.Deserialize(br, tp_elem);
                }
                catch (EndOfStreamException ex)
                {
                    if (strict)
                        throw new InvalidDataException("UniversalSequenceBase variable-size payload is truncated.", ex);
                    fs.Position = off;
                    break;
                }

                logicalEnd = fs.Position;
                count++;
            }

            if (strict && count != declaredCount)
                throw new InvalidDataException("UniversalSequenceBase variable-size payload is truncated.");

            nelements = count;
            append_offset = logicalEnd;

            if (fs.Length != append_offset)
                fs.SetLength(append_offset);

            if (rewriteHeader)
                Flush();
            else
                fs.Position = append_offset;
        }
    }
}
