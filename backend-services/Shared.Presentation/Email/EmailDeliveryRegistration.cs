using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Resend;

namespace Shared.Presentation.Email;

/// <summary>Registers Resend using configuration supplied by user secrets or production secret injection.</summary>
public static class EmailDeliveryRegistration
{
    public static IServiceCollection AddTransactionalEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddResend(options => options.ApiToken = configuration["Resend:ApiToken"] ?? string.Empty);
        services.AddOptions<ResendClientOptions>()
            .Validate(HasApiToken, "Configure Resend:ApiToken using user secrets or Resend__ApiToken.")
            .ValidateOnStart();
        services.AddTransient<IEmailDelivery, ResendEmailDelivery>();
        return services;
    }

    private static bool HasApiToken(ResendClientOptions options) => !string.IsNullOrWhiteSpace(options.ApiToken);
}
