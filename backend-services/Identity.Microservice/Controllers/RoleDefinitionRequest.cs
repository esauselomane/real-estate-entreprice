using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using System.Security.Claims;

namespace Identity.Microservice.Controllers
{
        public class RoleDefinitionRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool IsPlatformScoped { get; set; } = false;
            public bool IsAgencyScoped { get; set; } = false;
            public string PermissionsJson { get; set; } = "[]";
            public bool IsActive { get; set; } = true;
        }
}
