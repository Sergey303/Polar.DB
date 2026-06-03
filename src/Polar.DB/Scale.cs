namespace Polar.DB
{
    [Obsolete]
    public class Scale
    {
        private int keysLength, n_scale, min, max;
        public Func<int, Diapason> GetDia = null;
        private int[] starts = null;
        private Func<int, int> ToPosition = null;
        private UniversalSequenceBase keylengthminmaxstarts;

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
                for (int i = 3; i < nvalues; i++) starts[i - 3] = (int)keylengthminmaxstarts.GetByIndex(i);
                SetToPosition();
                SetGetDia();
            }
        }
        public void Close() { keylengthminmaxstarts.Close(); }
        public void Load(int[] keys)
        {
            keysLength = keys.Length;
            if (keysLength == 0)
            {
                GetDia = EmptyDiapasonResolverByInt;
                keylengthminmaxstarts.Clear();
                keylengthminmaxstarts.Flush();
                return;
            }
            n_scale = keysLength / 16;
            min = keys[0];
            max = keys[keysLength - 1];
            if (n_scale < 1 || min == max)
            {
                n_scale = 1;
                starts = new int[1];
                starts[0] = 0;
            }
            else starts = new int[n_scale];
            SetToPosition();
            for (int i = 0; i < keys.Length; i++) starts[ToPosition(keys[i])] += 1;
            int sum = 0;
            for (int i = 0; i < n_scale; i++) { int num = starts[i]; starts[i] = sum; sum += num; }
            SetGetDia();
            keylengthminmaxstarts.Clear();
            keylengthminmaxstarts.AppendElement(keysLength);
            keylengthminmaxstarts.AppendElement(min);
            keylengthminmaxstarts.AppendElement(max);
            for (int i = 0; i < starts.Length; i++) keylengthminmaxstarts.AppendElement(starts[i]);
            keylengthminmaxstarts.Flush();
        }
        private void SetToPosition()
        {
            if (starts.Length == 1) ToPosition = key => 0;
            else ToPosition = key => (int)(((long)key - min) * (n_scale - 1) / ((long)max - min));
        }
        private void SetGetDia()
        {
            GetDia = key =>
            {
                int ind = ToPosition(key);
                if (ind < 0 || ind >= n_scale) return Diapason.Empty;
                int sta = starts[ind];
                int num = ind < n_scale - 1 ? starts[ind + 1] - sta : keysLength - sta;
                return new Diapason { start = sta, numb = num };
            };
        }
        public static Func<int, Diapason> GetDiaFunc32(IEnumerable<int> keys, int min, int max, int n_scale)
        {
            if (keys == null) return EmptyDiapasonResolverByInt;
            int[] arr = keys.ToArray();
            if (arr.Length == 0) return EmptyDiapasonResolverByInt;
            return GetDiaFunc32(arr);
        }
        public static Func<int, Diapason> GetDiaFunc32(int[] keys)
        {
            if (keys == null || keys.Length == 0) return EmptyDiapasonResolverByInt;
            int N = keys.Length;
            int min = keys[0];
            int max = keys[N - 1];
            int n_scale = N / 16;
            int[] starts;
            Func<int, int> ToPosition;
            if (n_scale < 1 || min == max)
            {
                n_scale = 1; starts = new int[1]; starts[0] = 0; ToPosition = key => 0;
            }
            else
            {
                starts = new int[n_scale];
                ToPosition = key => (int)(((long)key - min) * (n_scale - 1) / ((long)max - min));
            }
            for (int i = 0; i < keys.Length; i++) starts[ToPosition(keys[i])] += 1;
            int sum = 0;
            for (int i = 0; i < n_scale; i++) { int num = starts[i]; starts[i] = sum; sum += num; }
            return key =>
            {
                int ind = ToPosition(key);
                if (ind < 0 || ind >= n_scale) return Diapason.Empty;
                int sta = starts[ind];
                int num = ind < n_scale - 1 ? starts[ind + 1] - sta : keys.Length - sta;
                return new Diapason { start = sta, numb = num };
            };
        }
        public static Func<long, Diapason> GetDiaFunc64(long[] keys)
        {
            if (keys == null || keys.Length == 0) return _ => Diapason.Empty;
            int N = keys.Length;
            long min = keys[0], max = keys[N - 1];
            int n_scale = N / 16;
            int[] starts;
            Func<long, int> ToPosition;
            if (n_scale < 1 || min == max) { n_scale = 1; starts = new int[1]; ToPosition = key => 0; }
            else { starts = new int[n_scale]; ToPosition = key => (int)((key - min) * (n_scale - 1) / (max - min)); }
            for (int i = 0; i < keys.Length; i++) starts[ToPosition(keys[i])] += 1;
            int sum = 0;
            for (int i = 0; i < n_scale; i++) { int num = starts[i]; starts[i] = sum; sum += num; }
            return key =>
            {
                int ind = ToPosition(key);
                if (ind < 0 || ind >= n_scale) return Diapason.Empty;
                int sta = starts[ind];
                int num = ind < n_scale - 1 ? starts[ind + 1] - sta : keys.Length - sta;
                return new Diapason { start = sta, numb = num };
            };
        }
        public static Func<int, Diapason> EmptyDiapasonResolverByInt = _ => Diapason.Empty;
    }
}
