using Bokmal.Api.Services;

namespace Bokmal.Tests;

/// <summary>
/// The reading-time estimate. Pure arithmetic over loan durations, so these are plain unit
/// tests -- but the arithmetic encodes a judgement call worth pinning down.
/// </summary>
public class ReadingTimeEstimatorTests
{
    private static TimeSpan[] Days(params double[] days) => [.. days.Select(TimeSpan.FromDays)];

    [Fact]
    public void The_estimate_is_the_median_of_how_long_borrowers_kept_it()
    {
        var estimate = ReadingTimeEstimator.Estimate(Days(6, 8, 10, 12, 14), pageCount: 300);

        Assert.True(estimate.FromHistory);
        Assert.Equal(10, estimate.TypicalDays);
        Assert.Equal(5, estimate.BasedOnLoans);
    }

    [Fact]
    public void One_borrower_who_kept_it_for_months_does_not_move_the_estimate()
    {
        // The whole reason for choosing a median. These are the same five readers as above
        // with one straggler swapped in: the average jumps from 10 days to 26, which would
        // tell every future borrower this is a two-month book. It is not.
        var withStraggler = ReadingTimeEstimator.Estimate(Days(6, 8, 10, 12, 94), pageCount: 300);

        Assert.Equal(10, withStraggler.TypicalDays);
        Assert.Equal(26, (int)Days(6, 8, 10, 12, 94).Average(d => d.TotalDays));
    }

    [Fact]
    public void An_even_number_of_loans_averages_the_middle_two()
    {
        var estimate = ReadingTimeEstimator.Estimate(Days(4, 8, 12, 16), pageCount: 300);

        Assert.Equal(10, estimate.TypicalDays);
    }

    [Fact]
    public void Too_little_history_falls_back_to_the_length_of_the_book()
    {
        var estimate = ReadingTimeEstimator.Estimate(Days(3, 4), pageCount: 350);

        Assert.False(estimate.FromHistory);
        Assert.Equal(2, estimate.BasedOnLoans);
        Assert.Equal((int)Math.Ceiling(350 / ReadingTimeEstimator.AssumedPagesPerDay), estimate.TypicalDays);
    }

    [Fact]
    public void A_book_nobody_has_finished_still_gets_an_estimate()
    {
        var estimate = ReadingTimeEstimator.Estimate([], pageCount: 700);

        Assert.False(estimate.FromHistory);
        Assert.Equal(0, estimate.BasedOnLoans);
        Assert.True(estimate.TypicalDays > 0);
    }

    [Fact]
    public void The_estimate_is_never_zero_days()
    {
        // A pamphlet returned within the hour would otherwise round to "usually out 0 days",
        // which reads as broken rather than as fast.
        var fromHistory = ReadingTimeEstimator.Estimate(Days(0.1, 0.2, 0.1, 0.3), pageCount: 20);
        var fromLength = ReadingTimeEstimator.Estimate([], pageCount: 1);

        Assert.Equal(1, fromHistory.TypicalDays);
        Assert.Equal(1, fromLength.TypicalDays);
    }
}
