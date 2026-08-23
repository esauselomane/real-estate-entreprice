namespace PlatformSyndication.Worker.Models
{
    // Structural deserialization template matching incoming syndication messages
    public class SyndicationTask
    {
        public int PropertyId { get; set; }
        public int PlatformId { get; set; }
        public bool IsActive { get; set; }
    }
}
