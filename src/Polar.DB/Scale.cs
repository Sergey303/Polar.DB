namespace Polar.DB
{
    /// <summary>
    /// Stores a coarse key distribution scale and provides fast candidate-range estimation.
    /// </summary>
    /// <remarks>
    /// The scale is persisted as integers: key count, minimum key, maximum key and bucket start positions.
    /// It is intended as a pre-filter before exact matching in sorted key collections.
    /// </remarks>
    public class Scale
    {
        private int keysLength;
        private int n_scale;
        private int min;
        private int max;

        /// <summary>
        /// Gets a function that maps a key sample to an approximate candidate diapason.
        /// </summary>
        public Func<int, Diapason> GetDia = _ => Diapason.Empty;

        private int[] starts = Array.Empty<int>();
        private Func<int, int> ToPosition = _ => 0;
        private readonly UniversalSequenceBase keylengthminmaxstarts;

        /// <summary>
        /// Opens a persisted scale from the provided stream.
        /// </summary>
        /// <param name="stream">Stream containing scale metadata.</param>
        public Scale(Stream stream)
        {
            keylengthminmaxstarts = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);
            int nvalues = (int)keylengthminmaxstarts.Count();
            if (nvalues > 0)
            {
                keysLength = (int)keylengthminmaxstarts.GetByIndex(0);
                min = (int)keylengthminmaxstarts.GetByIndex(1);
                max = (int)keylengthminmaxstarts.GetByIndex(2);
                n_scale = nvalues - 3;
                starts = new int[n_scale];
                for (int i = 3; i < nvalues; i++)
                {
                    starts[i - 3] = (int)keylengthminmaxstarts.GetByIndex(i);
                }

                SetToPosition();
                SetGetDia();
            }
        }

        /// <summary>
        /// Closes the scale metadata stream.
        /// </summary>
        public void Close()
        {
            keylengthminmaxstarts.Close();
        }

        /// <summary>
        /// Rebuilds the scale from a sorted key array and persists it.
        /// </summary>
        /// <param name="keys">Sorted key values used to build bucket boundaries.</param>
        public void Load(int[] keys)
        {
            keysLength = keys.Length;
            if (keysLength == 0) return;

            n_scale = keysLength / 16;
            min = keys[0];
            max = keys[keysLength - 1];

            if (n_scale < 1 || min == max)
            {
                n_scale = 1;
                starts = new int[1];
                starts[0] = 0;
            }
            else
            {
                starts = new int[n_scale];
            }

            SetToPosition();

            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
                int position = ToPosition(key);
                starts[position] += 1;
            }

            int sum = 0;
            for (int i = 0; i < n_scale; i++)
            {
                int num_els = starts[i];
                starts[i] = sum;
                sum += num_els;
            }

            SetGetDia();

            keylengthminmaxstarts.Clear();
            keylengthminmaxstarts.AppendElement(keysLength);
            keylengthminmaxstarts.AppendElement(min);
            keylengthminmaxstarts.AppendElement(max);
            for (int i = 0; i < starts.Length; i++)
            {
                keylengthminmaxstarts.AppendElement(starts[i]);
            }

            keylengthminmaxstarts.Flush();
        }

        private void SetToPosition()
        {
            if (starts.Length == 1)
                ToPosition = _ => 0;
            else
                ToPosition = key => (int)(((long)key - min) * (n_scale - 1L) / (max - (long)min));
        }

        private void SetGetDia()
        {
            GetDia = key =>
            {
                int ind = ToPosition(key);
                if (ind < 0 || ind >= n_scale)
                {
                    return Diapason.Empty;
                }

                int sta = starts[ind];
                int num = ind < n_scale - 1 ? starts[ind + 1] - sta : keysLength - sta;
                return new Diapason { start = sta, numb = num };
            };
        }

        /// <summary>
        /// Builds a 32-bit key-to-diapason estimator from an enumerable key sequence.
        /// </summary>
        /// <param name="keys">Sorted key flow.</param>
        /// <param name="min">Minimum key value in the flow.</param>
        /// <param name="max">Maximum key value in the flow.</param>
        /// <param name="n_scale">Requested bucket count, usually keyCount / 16.</param>
        /// <returns>Function that maps key samples to approximate candidate ranges.</returns>
        public static Func<int, Diapason> GetDiaFunc32(IEnumerable<int> keys, int min, int max, int n_scale)
        {
            int[] starts;
            Func<int, int> toPosition;

            if (n_scale < 1 || min == max)
            {
                n_scale = 1;
                starts = new int[1];
                starts[0] = 0;
                toPosition = _ => 0;
            }
            else
            {
                starts = new int[n_scale];
                toPosition = key => (int)(((long)key - min) * (n_scale - 1L) / (max - (long)min));
            }

            int keyCount = 0;
            foreach (var key in keys)
            {
                int position = toPosition(key);
                starts[position] += 1;
                keyCount++;
            }

            int sum = 0;
            for (int i = 0; i < n_scale; i++)
            {
                int num_els = starts[i];
                starts[i] = sum;
                sum += num_els;
            }

            return key =>
            {
                int ind = toPosition(key);
                if (ind < 0 || ind >= n_scale)
                {
                    return Diapason.Empty;
                }

                int sta = starts[ind];
                int num = ind < n_scale - 1 ? starts[ind + 1] - sta : keyCount - sta;
                return new Diapason { start = sta, numb = num };
            };
        }

        /// <summary>
        /// Builds a 32-bit key-to-diapason estimator from an in-memory key array.
        /// </summary>
        /// <param name="keys">Sorted key array.</param>
        /// <returns>Estimator function, or <see langword="null"/> when the source is empty.</returns>
        public static Func<int, Diapason> GetDiaFunc32(int[] keys)
        {
            if (keys == null || keys.Length == 0) return null!;

            int n = keys.Length;
            int min = keys[0];
            int max = keys[n - 1];
            int n_scale = n / 16;
            int[] starts;
            Func<int, int> toPosition;

            if (n_scale < 1 || min == max)
            {
                n_scale = 1;
                starts = new int[1];
                starts[0] = 0;
                toPosition = _ => 0;
            }
            else
            {
                starts = new int[n_scale];
                toPosition = key => (int)(((long)key - min) * (n_scale - 1L) / (max - (long)min));
            }

            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
                int position = toPosition(key);
                starts[position] += 1;
            }

            int sum = 0;
            for (int i = 0; i < n_scale; i++)
            {
                int num_els = starts[i];
                starts[i] = sum;
                sum += num_els;
            }

            return key =>
            {
                int ind = toPosition(key);
                if (ind < 0 || ind >= n_scale)
                {
                    return Diapason.Empty;
                }

                int sta = starts[ind];
                int num = ind < n_scale - 1 ? starts[ind + 1] - sta : keys.Length - sta;
                return new Diapason { start = sta, numb = num };
            };
        }

        /// <summary>
        /// Builds a 64-bit key-to-diapason estimator from an in-memory key array.
        /// </summary>
        /// <param name="keys">Sorted key array.</param>
        /// <returns>Estimator function, or <see langword="null"/> when the source is empty.</returns>
        public static Func<long, Diapason> GetDiaFunc64(long[] keys)
        {
            if (keys == null || keys.Length == 0) return null!;

            int n = keys.Length;
            long min = keys[0];
            long max = keys[n - 1];
            int n_scale = n / 16;
            int[] starts;
            Func<long, int> toPosition;

            if (n_scale < 1 || min == max)
            {
                n_scale = 1;
                starts = new int[1];
                starts[0] = 0;
                toPosition = _ => 0;
            }
            else
            {
                starts = new int[n_scale];
                toPosition = key => (int)((key - min) * (n_scale - 1L) / (max - min));
            }

            for (int i = 0; i < keys.Length; i++)
            {
                long key = keys[i];
                int position = toPosition(key);
                starts[position] += 1;
            }

            int sum = 0;
            for (int i = 0; i < n_scale; i++)
            {
                int num_els = starts[i];
                starts[i] = sum;
                sum += num_els;
            }

            return key =>
            {
                int ind = toPosition(key);
                if (ind < 0 || ind >= n_scale)
                {
                    return Diapason.Empty;
                }

                int sta = starts[ind];
                int num = ind < n_scale - 1 ? starts[ind + 1] - sta : keys.Length - sta;
                return new Diapason { start = sta, numb = num };
            };
        }
    }
}
