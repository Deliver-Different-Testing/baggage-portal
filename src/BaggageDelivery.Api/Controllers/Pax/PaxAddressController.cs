using BaggageDelivery.Core.AddressLookup;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax/{id}/address")]
[AllowAnonymous]
public sealed class PaxAddressController(
    IEncryptionService encryption,
    IAddressLookupService addressLookup,
    IOptions<DespatchOptions> despatchOptions) : ControllerBase
{
    [HttpGet("autocomplete")]
    public async Task<ActionResult<IReadOnlyList<AddressSearchResult>>> Autocomplete(
        string id,
        [FromQuery] string text,
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

        var results = await addressLookup.AutocompleteAsync(text, despatchOptions.Value.Countries, ct);
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
