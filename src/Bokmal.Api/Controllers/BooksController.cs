using Bokmal.Api.Contracts;
using Bokmal.Api.Identity;
using Bokmal.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bokmal.Api.Controllers;

/// <summary>
/// The catalogue. Books are addressed by slug rather than id: the identifiers are internal
/// and opaque, the slug is stable, readable and what the URL bar should show.
/// </summary>
[ApiController]
[Route("api/books")]
[Produces("application/json")]
public sealed class BooksController(
    CatalogueService catalogue,
    DiscoveryService discovery,
    ReadingTimeService readingTimes,
    ICurrentBorrower currentBorrower) : ControllerBase
{
    /// <summary>Browse the shelves, optionally filtered by a search term or a genre.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BookSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookSummaryDto>>> Browse(
        [FromQuery] string? search,
        [FromQuery] string? genre,
        CancellationToken cancellationToken)
    {
        var entries = await catalogue.BrowseAsync(search, genre, cancellationToken);

        return Ok(entries.Select(e => e.ToSummary()).ToList());
    }

    /// <summary>The genres present in the catalogue, for building a filter.</summary>
    [HttpGet("genres")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Genres(CancellationToken cancellationToken)
        => Ok(await catalogue.ListGenresAsync(cancellationToken));

    /// <summary>The most borrowed books of all time.</summary>
    [HttpGet("top")]
    [ProducesResponseType<IReadOnlyList<TopBookDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TopBookDto>>> Top(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var top = await discovery.TopBorrowedAsync(Math.Clamp(limit, 1, 50), cancellationToken);
        var estimates = await readingTimes.EstimateAsync(top.Select(t => t.Book.Id).ToList(), cancellationToken);

        return Ok(top
            .Select(t => new TopBookDto(
                DtoMapping.ToSummary(t.Book, t.Availability, estimates[t.Book.Id]),
                t.BorrowCount))
            .ToList());
    }

    /// <summary>
    /// Suggestions for the signed-in borrower, grouped by the recently finished book that
    /// prompted each one. Empty until they have returned something -- there is nothing
    /// honest to base a suggestion on before that.
    /// </summary>
    [HttpGet("for-me")]
    [RequireBorrower]
    [ProducesResponseType<IReadOnlyList<RecommendationGroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RecommendationGroupDto>>> ForMe(
        [FromQuery] int groups = 2,
        [FromQuery] int perGroup = 3,
        CancellationToken cancellationToken = default)
    {
        var borrower = await currentBorrower.RequireAsync(cancellationToken);

        var found = await discovery.ForBorrowerAsync(
            borrower.Id, Math.Clamp(groups, 1, 5), Math.Clamp(perGroup, 1, 10), cancellationToken);

        var bookIds = found
            .SelectMany(g => g.Recommendations.Select(r => r.Book.Id).Append(g.BasedOn.Id))
            .Distinct()
            .ToList();

        var estimates = await readingTimes.EstimateAsync(bookIds, cancellationToken);
        var shelf = await catalogue.AvailabilityForAsync(bookIds, cancellationToken);

        return Ok(found
            .Select(g => new RecommendationGroupDto(
                DtoMapping.ToSummary(g.BasedOn, shelf[g.BasedOn.Id], estimates[g.BasedOn.Id]),
                g.Recommendations
                    .Select(r => new RecommendationDto(
                        DtoMapping.ToSummary(r.Book, r.Availability, estimates[r.Book.Id]),
                        r.SharedBorrowers))
                    .ToList()))
            .ToList());
    }

    /// <summary>
    /// One book, with how many copies are on the shelf right now, how long it usually takes
    /// to read, and what its readers went on to borrow.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<BookDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDetailDto>> Get(string slug, CancellationToken cancellationToken)
    {
        var entry = await catalogue.FindAsync(slug, cancellationToken);

        if (entry is null)
            return NotFound();

        // Anonymous browsing is fine here -- the catalogue is public. A visitor simply gets
        // the unfiltered list, because there is nothing known about them to filter against.
        var borrower = await currentBorrower.GetAsync(cancellationToken);

        var recommendations = await discovery.RecommendationsForAsync(
            entry.Book.Id, borrower?.Id, limit: 5, cancellationToken);

        var estimates = await readingTimes.EstimateAsync(
            recommendations.Select(r => r.Book.Id).ToList(), cancellationToken);

        var alsoBorrowed = recommendations
            .Select(r => new RecommendationDto(
                DtoMapping.ToSummary(r.Book, r.Availability, estimates[r.Book.Id]),
                r.SharedBorrowers))
            .ToList();

        return Ok(entry.ToDetail(alsoBorrowed));
    }
}
