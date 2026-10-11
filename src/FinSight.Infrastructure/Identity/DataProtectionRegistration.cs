using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FinSight.Infrastructure.Persistence;

namespace FinSight.Infrastructure.Identity;

public static class DataProtectionRegistration
{
    public static IServiceCollection AddFinSightDataProtection(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var builder = services.AddDataProtection().SetApplicationName("FinSight");
        if (environment.IsDevelopment())
        {
            var keysPath = configuration["DataProtection:KeysPath"];
            if (string.IsNullOrWhiteSpace(keysPath))
                keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FinSight", "DataProtection-Keys");
            if (!Path.IsPathFullyQualified(keysPath))
                throw new InvalidOperationException("DataProtection:KeysPath must be an absolute persistent directory.");
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }
        else builder.PersistKeysToDbContext<FinSightDbContext>();

        var certificatePath = configuration["DataProtection:CertificatePath"];
        var certificateBase64Path = configuration["DataProtection:CertificateBase64File"];
        if (!string.IsNullOrWhiteSpace(certificatePath) || !string.IsNullOrWhiteSpace(certificateBase64Path))
        {
            // Render secret files are plaintext: accept base64 PFX without writing decoded
            // private-key material to the ephemeral filesystem.
            var certificate = !string.IsNullOrWhiteSpace(certificateBase64Path)
                ? X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(File.ReadAllText(certificateBase64Path)),
                    configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet)
                : X509CertificateLoader.LoadPkcs12FromFile(certificatePath!,
                    configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey) throw new InvalidOperationException("The Data Protection certificate must have a private key.");
            builder.ProtectKeysWithCertificate(certificate);
            var previousPaths = configuration.GetSection("DataProtection:PreviousCertificateBase64Files").Get<string[]>() ?? [];
            var previous = previousPaths.Select(path => X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(File.ReadAllText(path)), configuration["DataProtection:CertificatePassword"],
                X509KeyStorageFlags.EphemeralKeySet)).ToArray();
            builder.UnprotectKeysWithAnyCertificate([certificate, .. previous]);
        }
        else if (environment.IsDevelopment() && OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi();
        }
        else if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Configure DataProtection:CertificatePath or CertificateBase64File to encrypt the database key ring.");
        }
        return services;
    }
}
