using BaggageDelivery.Core.AddressLookup;
using BaggageDelivery.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax/{id}/address")]
[AllowAnonymous]
public sealed class PaxAddressController(
    IEncryptionService encryption,
    IAddressLookupService addressLookup) : ControllerBase
{
    [HttpGet("autocomplete")]
    public async Task<ActionResult<IReadOnlyList<AddressSearchResult>>> Autocomplete(
        string id,
        [FromQuery] string text,
        [FromQuery] string? countryCode,
        CancellationToken ct)
    {
        if (encryption.DecryptId(id) is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
        {
            return Ok(Array.Empty<AddressSearchResult>());
        }

        var results = await addressLookup.AutocompleteAsync(text, countryCode, ct);
        return Ok(results);
    }

    [HttpGet("lookup/{addressId}")]
    public async Task<ActionResult<AddressDetail>> Lookup(
        string id,
        string addressId,
        CancellationToken ct)
    {
        if (encryption.DecryptId(id) is null)
        {
            return NotFound();
        }

        var detail = await addressLookup.LookupAsync(addressId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }
}
