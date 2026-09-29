namespace Arlink28.Api.Features.Catalogue.ResponseModels;

public record PartnerResponse(Guid Id, string Slug, string Name, string? Tagline, string? LogoPath);
