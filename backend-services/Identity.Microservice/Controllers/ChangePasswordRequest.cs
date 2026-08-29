using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Microservice.Controllers
{
        public class ChangePasswordRequest
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
}
