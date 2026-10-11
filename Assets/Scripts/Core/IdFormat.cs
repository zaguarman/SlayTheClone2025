namespace SlayTheClone {
    public static class IdFormat {
        private const int ShortLength = 8;

        /// <summary>First characters of an id, upper-cased, for compact log lines.</summary>
        public static string ShortId(this string id) {
            if (id == null) return "UNKNOWN";
            return (id.Length > ShortLength ? id.Substring(0, ShortLength) : id).ToUpper();
        }
    }
}
