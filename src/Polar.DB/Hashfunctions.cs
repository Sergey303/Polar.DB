namespace Polar.DB
{
    /// <summary>
    /// Legacy hash helpers used by older indexing flows and samples.
    /// </summary>
    public static class Hashfunctions
    {
        /// <summary>
        /// Computes a 32-bit rolling hash that mixes low 8 bits of each character.
        /// </summary>
        /// <param name="str">Input string.</param>
        /// <returns>Deterministic hash value for the input string.</returns>
        /// <remarks>
        /// This function is preserved for compatibility and is not intended as a cryptographic hash.
        /// </remarks>
        public static int HashRot13(string str)
        {
            _ = str ?? throw new ArgumentNullException(nameof(str));
            uint hash = 0;
            foreach (char c in str)
            {
                hash += Convert.ToUInt32(c) & 255;
                hash -= (hash << 13) | (hash >> 19);
            }

            return (int)hash;
        }

        /// <summary>
        /// Encodes up to four leading characters into a compact sortable integer key.
        /// </summary>
        /// <param name="s">Input text.</param>
        /// <returns>7-bit packed key derived from the first four mapped characters.</returns>
        /// <remarks>
        /// Characters are mapped through a fixed alphabet that includes Latin symbols and an uppercase Russian subset.
        /// Unknown characters are mapped to the first symbol.
        /// </remarks>
        public static int First4charsRu(string s)
        {
            _ = s ?? throw new ArgumentNullException(nameof(s));
            const string schars = "!\"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHJKLMNOPQRSTUWXYZ[\\]^_`{|}~АБВГДЕЖЗИЙКЛМНОПРСТУФКЦЧШЩЪЫЬЭЮЯЁ";
            int len = s.Length;
            var chs = s.ToCharArray()
                .Concat(Enumerable.Repeat(' ', len < 4 ? 4 - len : 0))
                .Take(4)
                .Select(ch =>
                {
                    int ind = schars.IndexOf(char.ToUpper(ch));
                    if (ind == -1) ind = 0;
                    return ind;
                })
                .ToArray();

            return ((((((chs[0] << 7) | chs[1]) << 7) | chs[2]) << 7) | chs[3]);
        }
    }
}
