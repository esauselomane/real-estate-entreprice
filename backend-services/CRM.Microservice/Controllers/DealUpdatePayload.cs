namespace CRM.Microservice.Controllers
{
        public class DealUpdatePayload { public int DealId { get; set; } public string NextStage { get; set; } = string.Empty; public string NotesUpdate { get; set; } = string.Empty; }

        // 📝 Payload for `POST /crm/deal/{id}/notes` — appends a timestamped
        // entry to the deal's DealNotes without requiring the caller to first
        // fetch (read) the existing notes (the PUT overwrite would clobber them).
        public class DealNotePayload { public string NoteText { get; set; } = string.Empty; }
}
