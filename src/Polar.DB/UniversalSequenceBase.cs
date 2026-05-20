using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
//using Polar.DB;

namespace Polar.DB
{
    /// <summary>
    /// Класс предоставляет последовательность элементов типа tp_elem, опирающуюся на индекс или полуиндекс ключей типа Tkey
    /// </summary>
    public class UniversalSequenceBase
    {
        protected PType tp_elem; // Поляровский тип элемента
        protected Stream fs; // Стрим - среда для последовательности. Сначала 8 байтов длина, потом подряд бинарные развертки элементов 
        internal Stream Media { get { return fs; } }
        public long AppendOffset => ElementOffset();

        private BinaryReader br;
        private BinaryWriter bw;
        protected int elem_size = -1; // длина элемента, если она фиксирована, иначе -1
        private long nelements; // текущее количество элеметов. В "покое" - совпадает со значением в первых 8 байтах 

        public UniversalSequenceBase(PType tp_el, Stream media)
        {
            tp_elem = tp_el;
            if (tp_elem.HasNoTail) elem_size = tp_elem.HeadSize;
            fs = media;
            br = new BinaryReader(fs);
            bw = new BinaryWriter(fs);

            if (fs.Length == 0)
            {
                Clear();
            }
            else
            {
                RecoverFromStream();
            }
        }

        private void RecoverFromStream()
        {
            if (fs.Length < 8L)
            {
                throw new InvalidDataException("UniversalSequenceBase header is truncated.");
            }

            fs.Position = 0L;
            long declaredCount = br.ReadInt64();
            if (declaredCount < 0L)
            {
                throw new InvalidDataException("UniversalSequenceBase header contains a negative element count.");
            }

            if (elem_size > 0)
            {
                RecoverFixedSize(declaredCount);
            }
            else
            {
                RecoverVariableSize(declaredCount);
            }

            fs.Position = append_offset;
        }

        private void RecoverFixedSize(long declaredCount)
        {
            long payloadBytes = fs.Length - 8L;
            long readableCount = payloadBytes / elem_size;
            nelements = Math.Min(declaredCount, readableCount);
            append_offset = 8L + nelements * elem_size;

            if (fs.Length != append_offset)
            {
                fs.SetLength(append_offset);
            }

            Flush();
        }

        private void RecoverVariableSize(long declaredCount)
        {
            long restoredCount = 0L;
            long logicalEnd = 8L;
            fs.Position = 8L;

            for (long i = 0L; i < declaredCount; i++)
            {
                long before = fs.Position;
                if (before >= fs.Length) break;

                try
                {
                    ByteFlow.Deserialize(br, tp_elem);
                    restoredCount++;
                    logicalEnd = fs.Position;
                }
                catch
                {
                    fs.Position = before;
                    break;
                }
            }

            nelements = restoredCount;
            append_offset = logicalEnd;

            if (fs.Length != append_offset)
            {
                fs.SetLength(append_offset);
            }

            Flush();
        }

        /// <summary>
        /// Делает последовательность с нулевым количеством элементов
        /// </summary>
        public void Clear()
        {
            fs.Position = 0L; fs.SetLength(0);
            bw.Write(0L);
            nelements = 0;
            append_offset = 8L;
            fs.Flush();
        }

        public void Flush()
        {
            long pos = fs.Position;
            fs.Position = 0L;
            bw.Write(nelements);
            fs.Position = pos;
            fs.Flush();
        }

        public void Close()
        {
            Flush();
            fs.Close();
        }

        public void Refresh()
        {
            if (fs.Length < 8L)
            {
                throw new InvalidDataException("UniversalSequenceBase header is truncated.");
            }

            fs.Position = 0L;
            long declaredCount = br.ReadInt64();
            if (declaredCount < 0L)
            {
                throw new InvalidDataException("UniversalSequenceBase header contains a negative element count.");
            }

            if (elem_size > 0)
            {
                long payloadBytes = fs.Length - 8L;
                long expectedPayloadBytes = declaredCount * elem_size;
                if (payloadBytes != expectedPayloadBytes)
                {
                    throw new InvalidDataException("UniversalSequenceBase fixed-size payload length does not match declared count.");
                }

                nelements = declaredCount;
                append_offset = 8L + expectedPayloadBytes;
                fs.Position = append_offset;
            }
            else
            {
                RecoverVariableSize(declaredCount);
            }
        }

        public long Count() { return nelements; }

        public long ElementOffset(long ind)
        {
            if (ind == 0L) return 8;
            if (ind < 0 || ind > nelements || !tp_elem.HasNoTail) throw new Exception("Err in ElementOffset");
            return 8 + ind * elem_size;
        }

        public long ElementOffset() { return append_offset; }

        /// <summary>
        /// Запись сериализации значения с текущей позиции. Корректна только если либо значение фиксированного размера, либо запись ведется в конец
        /// </summary>
        /// <param name="v"></param>
        /// <returns>позиция с которой началась запись</returns>
        public long SetElement(object v)
        {
            long pos = fs.Position;
            ByteFlow.Serialize(bw, v, tp_elem);
            return pos;
        }

        public void SetElement(object v, long off)
        {
            if (off > append_offset)
            {
                throw new InvalidOperationException("SetElement cannot write after the logical end of sequence.");
            }

            long originalPosition = fs.Position;
            long originalLength = fs.Length;
            long originalAppendOffset = append_offset;

            try
            {
                if (off != fs.Position) fs.Position = off;
                ByteFlow.Serialize(bw, v, tp_elem);

                if (off < originalAppendOffset && fs.Position > originalAppendOffset)
                {
                    throw new InvalidOperationException("SetElement crossed the logical end of sequence.");
                }
            }
            catch
            {
                if (fs.Length != originalLength)
                {
                    fs.SetLength(originalLength);
                }

                append_offset = originalAppendOffset;
                fs.Position = Math.Min(originalPosition, fs.Length);
                throw;
            }
        }

        public void SetTypedElement(PType tp, object v, long off)
        {
            if (off != fs.Position) fs.Position = off;
            ByteFlow.Serialize(bw, v, tp);
        }

        private long append_offset = 8L;

        public long AppendElement(object v)
        {
            long originalPosition = fs.Position;
            long originalLength = fs.Length;
            long originalAppendOffset = append_offset;
            long originalCount = nelements;
            long off = append_offset;

            try
            {
                if (fs.Position != off) fs.Position = off;
                ByteFlow.Serialize(bw, v, tp_elem);
                append_offset = fs.Position;
                nelements = originalCount + 1L;

                if (fs.Length != append_offset)
                {
                    fs.SetLength(append_offset);
                }

                return off;
            }
            catch
            {
                nelements = originalCount;
                append_offset = originalAppendOffset;

                if (fs.Length != originalLength)
                {
                    fs.SetLength(originalLength);
                }

                fs.Position = Math.Min(originalPosition, fs.Length);
                throw;
            }
        }

        public object GetElement()
        {
            return ByteFlow.Deserialize(br, tp_elem);
        }

        public object GetElement(long off)
        {
            if (off != fs.Position) fs.Position = off;
            return GetElement();
        }

        public object GetTypedElement(PType tp, long off)
        {
            if (off != fs.Position) fs.Position = off;
            return ByteFlow.Deserialize(br, tp);
        }

        public object GetByIndex(long index)
        {
            if (elem_size <= 0) throw new Exception("Err: method can't be implemented to sequences of unknown element size");
            if (index < 0 || index >= nelements) throw new IndexOutOfRangeException();
            return GetElement(ElementOffset(index));
        }

        public IEnumerable<object> ElementValues()
        {
            fs.Position = 8L;
            for (long i = 0; i < Count(); i++)
            {
                yield return GetElement();
            }
        }

        public IEnumerable<object> ElementValues(long offset, long number)
        {
            fs.Position = offset;
            for (long i = 0; i < number; i++)
            {
                yield return GetElement();
            }
        }

        // Основной сканер: быстро пробегаем по элементам, обрабатываем пары (offset, pobject) хендлером, хендлер возвращает true
        public void Scan(Func<long, object, bool> handler)
        {
            long ll = this.Count();
            if (ll == 0) return;
            fs.Position = 8L;
            for (long ii = 0; ii < ll; ii++)
            {
                long off = fs.Position;
                object pobject = GetElement();
                bool ok = handler(off, pobject);
                if (!ok) break;
            }
        }

        public IEnumerable<Tuple<long, object>> ElementOffsetValuePairs()
        {
            fs.Position = 8L;
            for (long i = 0; i < Count(); i++)
            {
                long off = fs.Position;
                object pobject = GetElement();
                yield return new Tuple<long, object>(off, pobject);
            }
        }

        public IEnumerable<Tuple<long, object>> ElementOffsetValuePairs(long offset, long number)
        {
            fs.Position = offset;
            for (long i = 0; i < number; i++)
            {
                long off = fs.Position;
                object pobject = GetElement();
                yield return new Tuple<long, object>(off, pobject);
            }
        }

        /// Если размер элемента фиксированный и есть функция ключа с целочисленным значением
        /// TODO: Вроде S32 вполне может работать для произвольных записей, но только на полном диапазоне.

        public void Sort32(Func<object, int> keyFun)
        {
            if (!tp_elem.HasNoTail || keyFun == null) throw new Exception("Err in Sort32:");
            S32(0, this.Count(), keyFun);
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
            // TODO: Похоже, метод работает правильно только для полного диапазона. 
            Clear();
            for (long ii = 0; ii < keys.LongLength; ii++)
            {
                AppendElement(records[ii]);
            }
            this.Flush();
        }

        /// <summary>
        /// Функция сортировки последовательности с использованием 64-разрядного ключа
        /// </summary>
        /// <param name="keyFun"></param>
        public void Sort64(Func<object, long> keyFun)
        {
            if (!tp_elem.HasNoTail || keyFun == null) throw new Exception("Err in Sort64:");
            S64(0, this.Count(), keyFun);
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
            // TODO: Похоже, метод работает правильно только для полного диапазона. 
            Clear();
            for (long ii = 0; ii < keys.LongLength; ii++)
            {
                AppendElement(records[ii]);
            }
            this.Flush();
        }
    }
}
