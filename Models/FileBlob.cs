namespace e_Commerce_application.Models
{
    // An uploaded file kept in the database, for hosts without a persistent disk.
    public class FileBlob
    {
        public int Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
