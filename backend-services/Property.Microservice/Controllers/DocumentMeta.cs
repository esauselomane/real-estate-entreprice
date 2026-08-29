using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Property.Microservice.Controllers
{
        public class DocumentMeta
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;   // original name (for download / display)
            public string StoredName { get; set; } = string.Empty;  // on-disk file name (for retrieval)
            public string ContentType { get; set; } = string.Empty;
            public long SizeBytes { get; set; }
            public DateTime UploadedAt { get; set; }
        }
}
