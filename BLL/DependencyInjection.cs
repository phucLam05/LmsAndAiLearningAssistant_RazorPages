using BLL.Interfaces;
using BLL.Services;

using Microsoft.Extensions.DependencyInjection;

namespace BLL
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBusinessLogicLayer(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IChunkingConfigService, ChunkingConfigService>();
            services.AddScoped<IChunkingService, ChunkingService>();
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<IDocumentConflictService, DocumentConflictService>();
            services.AddScoped<IEmbeddingService, DocumentEmbeddingService>();
            services.AddScoped<ISubjectService, SubjectService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IUserService, UserService>();

            return services;
        }
    }
}
