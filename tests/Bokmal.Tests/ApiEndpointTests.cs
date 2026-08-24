using System.Net;
using System.Net.Http.Json;
using Bokmal.Api.Contracts;
using Bokmal.Api.Controllers;
using Bokmal.Database;
using Bokmal.Database.Entities;
using Bokmal.Tests.Databases;
using Microsoft.AspNetCore.Mvc;

namespace Bokmal.Tests;

/// <summary>
/// The HTTP layer.
///
/// The services are tested directly elsewhere. What these cover is everything between a
/// request and a service call: which outcome becomes which status code, whether an anonymous
/// request is turned away, and whether the DTOs carry what the frontend needs. All of that is
/// real branching, and none of it is exercised by calling a service in-process.
/// </summary>
public class ApiEndpointTests
{
    private const string Astrid = "astrid@example.se";
    private const string Bjorn = "bjorn@example.se";

    private static Task<Library> ALibraryAsync() => Library.WithAsync(b => b
        .Book("dune", copies: 2)
        .Book("neuromancer", copies: 1)
        .Borrower(Astrid)
        .Borrower(Bjorn));

    private static async Task<LoanDto> BorrowAsync(Library library, HttpClient client, string slug)
    {
        var response = await client.PostAsJsonAsync(
            "/api/loans", new BorrowRequest(await library.BookIdAsync(slug)));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LoanDto>())!;
    }

    // ---------------------------------------------------------------- borrowing

    [Fact]
    public async Task Borrowing_returns_201_and_the_loan()
    {
        using var library = await ALibraryAsync();
        var client = library.CreateApiClient(Astrid);

        var response = await client.PostAsJsonAsync("/api/loans", new BorrowRequest(await library.BookIdAsync("dune")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var loan = await response.Content.ReadFromJsonAsync<LoanDto>();
        Assert.Equal("dune", loan!.BookSlug);
        Assert.Null(loan.ReturnedAt);
        Assert.False(loan.IsOverdue);
        Assert.Equal(loan.BorrowedAt.AddDays(LoanPolicy.LoanPeriodDays), loan.DueAt);
    }

    [Fact]
    public async Task Borrowing_the_same_book_twice_returns_409_with_a_reason()
    {
        using var library = await ALibraryAsync();
        var client = library.CreateApiClient(Astrid);

        await BorrowAsync(library, client, "dune");
        var response = await client.PostAsJsonAsync("/api/loans", new BorrowRequest(await library.BookIdAsync("dune")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // 409 rather than 400: the request was fine, the library's state was not. The
        // frontend shows `detail` to the borrower, so it has to be a sentence.
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(LoansController.AlreadyBorrowedTitle, problem!.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public async Task Borrowing_past_the_loan_limit_returns_409()
    {
        using var library = await Library.WithAsync(builder =>
        {
            builder.Borrower(Astrid);
            for (var i = 1; i <= LoanPolicy.MaxActiveLoansPerBorrower + 1; i++)
                builder.Book($"book-{i}", copies: 1);
        });

        var client = library.CreateApiClient(Astrid);

        for (var i = 1; i <= LoanPolicy.MaxActiveLoansPerBorrower; i++)
            await BorrowAsync(library, client, $"book-{i}");

        var response = await client.PostAsJsonAsync(
            "/api/loans",
            new BorrowRequest(await library.BookIdAsync($"book-{LoanPolicy.MaxActiveLoansPerBorrower + 1}")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(LoansController.LoanLimitReachedTitle,
            (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Borrowing_a_book_that_is_all_out_returns_409()
    {
        using var library = await ALibraryAsync();

        await BorrowAsync(library, library.CreateApiClient(Astrid), "neuromancer");

        var response = await library.CreateApiClient(Bjorn)
            .PostAsJsonAsync("/api/loans", new BorrowRequest(await library.BookIdAsync("neuromancer")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(LoansController.AllCopiesOutTitle,
            (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Borrowing_a_book_the_library_does_not_have_returns_404()
    {
        using var library = await ALibraryAsync();

        var response = await library.CreateApiClient(Astrid)
            .PostAsJsonAsync("/api/loans", new BorrowRequest(BokmalId.New()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------- returning

    [Fact]
    public async Task Returning_returns_200_and_closes_the_loan()
    {
        using var library = await ALibraryAsync();
        var client = library.CreateApiClient(Astrid);

        var loan = await BorrowAsync(library, client, "dune");
        var response = await client.PostAsync($"/api/loans/{loan.Id}/return", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull((await response.Content.ReadFromJsonAsync<LoanDto>())!.ReturnedAt);
    }

    [Fact]
    public async Task Returning_the_same_loan_twice_returns_409()
    {
        using var library = await ALibraryAsync();
        var client = library.CreateApiClient(Astrid);

        var loan = await BorrowAsync(library, client, "dune");
        await client.PostAsync($"/api/loans/{loan.Id}/return", null);

        var response = await client.PostAsync($"/api/loans/{loan.Id}/return", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Returning_somebody_elses_loan_returns_404_rather_than_403()
    {
        // 403 would confirm the id exists. Cheap to avoid, so avoided.
        using var library = await ALibraryAsync();

        var loan = await BorrowAsync(library, library.CreateApiClient(Astrid), "dune");

        var response = await library.CreateApiClient(Bjorn)
            .PostAsync($"/api/loans/{loan.Id}/return", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------- identity

    [Theory]
    [InlineData("/api/loans/me")]
    [InlineData("/api/session")]
    public async Task Endpoints_that_need_a_borrower_return_401_without_one(string path)
    {
        using var library = await ALibraryAsync();

        var response = await library.CreateApiClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_address_is_not_a_borrower()
    {
        using var library = await ALibraryAsync();

        var refused = await library.CreateApiClient("nobody@example.se").GetAsync("/api/loans/me");
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        var signIn = await library.CreateApiClient()
            .PostAsJsonAsync("/api/session", new SignInRequest("nobody@example.se"));
        Assert.Equal(HttpStatusCode.NotFound, signIn.StatusCode);
    }

    [Fact]
    public async Task An_empty_address_is_rejected_before_it_reaches_the_database()
    {
        using var library = await ALibraryAsync();

        var response = await library.CreateApiClient()
            .PostAsJsonAsync("/api/session", new SignInRequest("   "));

        // 400 rather than 404: an empty address is a malformed request, not a member who
        // happens not to exist.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_member_list_is_available_to_a_visitor_who_is_not_signed_in()
    {
        // It has to be. With no passwords it is the only way to discover a valid address,
        // and the sign-in page is by definition reached before signing in.
        using var library = await ALibraryAsync();

        var members = await library.CreateApiClient()
            .GetFromJsonAsync<List<BorrowerDto>>("/api/borrowers");

        Assert.Equal([Astrid, Bjorn], members!.Select(m => m.Email).Order());
    }

    [Fact]
    public async Task Signing_in_is_case_insensitive_about_the_address()
    {
        using var library = await ALibraryAsync();

        var response = await library.CreateApiClient()
            .PostAsJsonAsync("/api/session", new SignInRequest("  ASTRID@Example.SE  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Astrid, (await response.Content.ReadFromJsonAsync<BorrowerDto>())!.Email);
    }

    // ---------------------------------------------------------------- reading

    [Fact]
    public async Task My_loans_separates_what_is_out_from_what_has_been_returned()
    {
        using var library = await ALibraryAsync();
        var client = library.CreateApiClient(Astrid);

        var returned = await BorrowAsync(library, client, "dune");
        await client.PostAsync($"/api/loans/{returned.Id}/return", null);
        await BorrowAsync(library, client, "neuromancer");

        var mine = await client.GetFromJsonAsync<MyLoansDto>("/api/loans/me");

        Assert.Equal("neuromancer", Assert.Single(mine!.Current).BookSlug);
        Assert.Equal("dune", Assert.Single(mine.Past).BookSlug);
    }

    [Fact]
    public async Task A_book_reports_the_availability_the_borrower_needs_to_decide()
    {
        using var library = await ALibraryAsync();

        await BorrowAsync(library, library.CreateApiClient(Astrid), "dune");

        var book = await library.CreateApiClient().GetFromJsonAsync<BookDetailDto>("/api/books/dune");

        Assert.Equal(2, book!.Availability.TotalCopies);
        Assert.Equal(1, book.Availability.AvailableCopies);
        Assert.Equal(1, book.Availability.OnLoanCopies);
        Assert.True(book.Availability.IsAvailable);
    }

    [Fact]
    public async Task An_unknown_slug_is_a_404_and_the_catalogue_is_public()
    {
        using var library = await ALibraryAsync();
        var anonymous = library.CreateApiClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/books/nope")).StatusCode);

        // Browsing needs no borrower. Only acting on a loan does.
        var books = await anonymous.GetFromJsonAsync<List<BookSummaryDto>>("/api/books");
        Assert.Equal(2, books!.Count);
    }

    [Fact]
    public async Task The_top_list_is_ranked_and_public()
    {
        using var library = await ALibraryAsync();
        var client = library.CreateApiClient(Astrid);

        await BorrowAsync(library, client, "dune");

        // Anonymous: discovery is part of browsing, not something you sign in for.
        var top = await library.CreateApiClient()
            .GetFromJsonAsync<List<TopBookDto>>("/api/books/top?limit=10");

        Assert.Equal("dune", top![0].Book.Slug);
        Assert.Equal(1, top[0].BorrowCount);
        Assert.Equal(0, top[1].BorrowCount);
    }

    [Fact]
    public async Task The_genre_list_offers_what_the_catalogue_actually_holds()
    {
        // The filter is built from this, so it must not offer a genre with nothing behind it.
        using var library = await Library.WithAsync(b => b
            .Book("dune", copies: 1, genre: "Science Fiction")
            .Book("neuromancer", copies: 1, genre: "Science Fiction")
            .Book("beloved", copies: 1, genre: "Literary Fiction")
            .Borrower(Astrid));

        var genres = await library.CreateApiClient()
            .GetFromJsonAsync<List<string>>("/api/books/genres");

        Assert.Equal(["Literary Fiction", "Science Fiction"], genres);
    }

    [Fact]
    public async Task A_personal_shelf_needs_a_borrower()
    {
        using var library = await ALibraryAsync();

        var response = await library.CreateApiClient().GetAsync("/api/books/for-me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_member_who_has_finished_nothing_gets_an_empty_personal_shelf()
    {
        // The newcomer's view. An empty list rather than an error: there is nothing wrong,
        // there is just nothing to say yet, and the interface renders no section at all.
        using var library = await ALibraryAsync();

        var shelf = await library.CreateApiClient(Astrid)
            .GetFromJsonAsync<List<RecommendationGroupDto>>("/api/books/for-me");

        Assert.Empty(shelf!);
    }

    [Fact]
    public async Task Searching_matches_title_and_author_regardless_of_case()
    {
        using var library = await ALibraryAsync();
        var client = library.CreateApiClient();

        var byTitle = await client.GetFromJsonAsync<List<BookSummaryDto>>("/api/books?search=DUNE");
        Assert.Equal("dune", Assert.Single(byTitle!).Slug);

        var byAuthor = await client.GetFromJsonAsync<List<BookSummaryDto>>("/api/books?search=author");
        Assert.Equal(2, byAuthor!.Count);

        var noMatch = await client.GetFromJsonAsync<List<BookSummaryDto>>("/api/books?search=zzzz");
        Assert.Empty(noMatch!);
    }
}
