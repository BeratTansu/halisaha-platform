using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Halisaha.Api.OpenApi;

/// <summary>
/// OpenAPI dokümanına JWT Bearer şemasını ekler ve token isteyen işlemleri işaretler. Scalar bu bilgiyle token
/// yapıştırma alanı gösterir ve yalnızca gereken isteklere <c>Authorization</c> başlığı ekler.
/// Bir işlem token ister mi? Uygulamadaki kuralla aynı: <c>[AllowAnonymous]</c> yoksa ister (fallback policy).
/// </summary>
internal sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "POST /api/auth/login cevabındaki accessToken.",
        };
        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
        });
        return Task.CompletedTask;
    }
}
