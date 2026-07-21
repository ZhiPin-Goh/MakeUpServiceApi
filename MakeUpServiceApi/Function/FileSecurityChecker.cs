namespace CarRentalSystem_API.Function
{
    public static class FileSecurityChecker
    {
        private static readonly Dictionary<string, byte[]> ImageSignatures = new Dictionary<string, byte[]>
        {
            { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
            { ".jpg", new byte[] { 0xFF, 0xD8, 0xFF } },
            { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            { ".gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } }
        };

        public static bool IsValidImage(IFormFile file, string ext)
        {
            if (file == null || file.Length == 0)
                return false;

            if (!ImageSignatures.ContainsKey(ext))
                return false;

            using (var reader = new BinaryReader(file.OpenReadStream()))
            {
                var signatures = ImageSignatures[ext];
                var headerBytes = reader.ReadBytes(signatures.Length);

                for (int i = 0; i < signatures.Length; i++)
                {
                    if (headerBytes[i] != signatures[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
