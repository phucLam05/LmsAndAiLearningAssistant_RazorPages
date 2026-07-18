using DAL.Data;
using DAL.Interfaces;
using DAL.Providers;
using DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DAL
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
        {

            // AuditInterceptor only manages timestamps and has no web-layer dependency.
            services.AddScoped<AuditInterceptor>();

            // Use the (IServiceProvider, DbContextOptionsBuilder) overload to resolve Scoped interceptor
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    o => o.UseVector());
                options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
            });

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
            services.AddScoped<IChatMessageRepository, ChatMessageRepository>();

            services.AddHttpClient<ISupabaseStorageProvider, SupabaseStorageProvider>();
            services.AddHttpClient<IGeminiEmbeddingProvider, GeminiEmbeddingProvider>();
            services.AddHttpClient<IGeminiChatProvider, GeminiChatProvider>();
            services.AddScoped<IEmailSenderProvider, SmtpProvider>();

            return services;
        }
    }
}
