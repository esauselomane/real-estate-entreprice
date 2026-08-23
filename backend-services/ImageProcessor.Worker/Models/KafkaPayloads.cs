namespace ImageProcessor.Worker.Models
{
    // Structural deserialization template matching incoming message bus elements
    public class PropertyImageTask
    {
        public int PropertyId { get; set; }
        public string FileName { get; set; } = string.Empty;
        
        // Master binary image payload passed through network channels
        public string Base64Data { get; set; } = string.Empty; 
    }
}
