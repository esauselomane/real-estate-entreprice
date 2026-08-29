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
        public class DocumentUploadPayload
        {
            [Required] public string Name { get; set; } = string.Empty;        // human-friendly document name
            public string Description { get; set; } = string.Empty;             // free-form document description
            [Required] public string FileName { get; set; } = string.Empty;    // original file name from the client
            public string ContentType { get; set; } = string.Empty;
            [Required] public string Base64DataString { get; set; } = string.Empty; // raw file bytes, base64-encoded (mirrors image upload)
        }
}
