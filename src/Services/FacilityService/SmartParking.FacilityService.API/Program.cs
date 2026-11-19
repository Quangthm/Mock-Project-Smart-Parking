using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.API.Middleware;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Persistence;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Application.Reservations;

namespace SmartParking.FacilityService.API;

public partial class Program { public static void Main(string[] args) => FacilityHost.Build(args).Run(); }
