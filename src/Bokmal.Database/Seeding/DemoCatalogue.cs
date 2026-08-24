namespace Bokmal.Database.Seeding;

/// <summary>
/// How briskly a title moves off the shelf. Drives how long a copy sits between loans
/// when the demo history is generated, which is what gives the top list a real ranking
/// instead of noise.
/// </summary>
public enum Demand
{
    Quiet,
    Steady,
    Popular
}

public static class Genres
{
    public const string SciFiFantasy = "Science Fiction & Fantasy";
    public const string Crime = "Crime";
    public const string Literary = "Literary Fiction";
    public const string Classics = "Classics";
    public const string NonFiction = "Non-fiction";
}

public sealed record DemoBook(
    string Slug,
    string Title,
    string Author,
    string Genre,
    int PublishedYear,
    int PageCount,
    int Copies,
    Demand Demand,
    string Description);

/// <summary>
/// A borrower with a reading taste. The taste is the whole point: it leans towards one
/// genre and dips into a second, and that is what makes "others who borrowed this also
/// borrowed..." mean something. Draw loans uniformly at random instead and every
/// recommendation is technically correct and completely useless.
/// </summary>
/// <param name="JustJoined">
/// A member with no borrowing history at all. Every other demo borrower has read a dozen
/// books, which means the app can only ever be seen through the eyes of a regular -- and
/// the parts that greet a newcomer, the empty "For you" shelf and the empty loan list,
/// become unreachable. A real library always has somebody who joined last week.
/// </param>
public sealed record DemoBorrower(
    string Email,
    string DisplayName,
    string FavouriteGenre,
    string SecondGenre,
    bool JustJoined = false);

/// <summary>
/// The contents of the demo library. Reference data, written out plainly so it can be read
/// and changed without running anything.
/// </summary>
public static class DemoCatalogue
{
    public static IReadOnlyList<DemoBook> Books { get; } =
    [
        new("the-hobbit", "The Hobbit", "J.R.R. Tolkien", Genres.SciFiFantasy, 1937, 310, 4, Demand.Popular,
            "Bilbo Baggins is talked out of his comfortable hole and into a journey across Middle-earth to help a company of dwarves reclaim a stolen mountain."),
        new("dune", "Dune", "Frank Herbert", Genres.SciFiFantasy, 1965, 412, 4, Demand.Popular,
            "On the desert planet Arrakis, control of the galaxy's most valuable substance turns a noble house's exile into a war of prophecy and ecology."),
        new("neuromancer", "Neuromancer", "William Gibson", Genres.SciFiFantasy, 1984, 271, 2, Demand.Steady,
            "A burned-out console cowboy is offered his nervous system back in exchange for one last run against an artificial intelligence."),
        new("the-left-hand-of-darkness", "The Left Hand of Darkness", "Ursula K. Le Guin", Genres.SciFiFantasy, 1969, 304, 2, Demand.Steady,
            "An envoy from a galactic federation arrives on a frozen world whose inhabitants have no fixed sex, and finds every assumption he brought useless."),
        new("a-wizard-of-earthsea", "A Wizard of Earthsea", "Ursula K. Le Guin", Genres.SciFiFantasy, 1968, 183, 3, Demand.Steady,
            "A gifted boy on an island of mages lets his pride loose something into the world, and must sail to the edge of it to face what he made."),
        new("snow-crash", "Snow Crash", "Neal Stephenson", Genres.SciFiFantasy, 1992, 440, 2, Demand.Quiet,
            "A pizza-delivering hacker and a teenage courier chase a drug that works on computers and people alike through a privatised America."),
        new("the-fifth-season", "The Fifth Season", "N.K. Jemisin", Genres.SciFiFantasy, 2015, 468, 3, Demand.Steady,
            "A continent tears itself apart on schedule, and the people who can quiet the earth are the ones everyone else fears most."),

        new("the-girl-with-the-dragon-tattoo", "The Girl with the Dragon Tattoo", "Stieg Larsson", Genres.Crime, 2005, 465, 5, Demand.Popular,
            "A disgraced journalist and a brilliant, damaged investigator dig into a forty-year-old disappearance inside a wealthy Swedish family."),
        new("faceless-killers", "Faceless Killers", "Henning Mankell", Genres.Crime, 1991, 285, 3, Demand.Steady,
            "A double murder on an isolated farm in Skane leaves Inspector Wallander one whispered word to work from, and a town ready to blame anyone."),
        new("the-laughing-policeman", "The Laughing Policeman", "Maj Sjowall & Per Wahloo", Genres.Crime, 1968, 211, 2, Demand.Quiet,
            "Nine passengers are shot dead on a Stockholm bus, one of them a detective, and Martin Beck must work out which of them was the target."),
        new("gone-girl", "Gone Girl", "Gillian Flynn", Genres.Crime, 2012, 415, 3, Demand.Popular,
            "A wife vanishes on the morning of her fifth anniversary, and the story her husband tells stops matching the story her diary tells."),
        new("the-silence-of-the-lambs", "The Silence of the Lambs", "Thomas Harris", Genres.Crime, 1988, 338, 2, Demand.Steady,
            "A trainee agent bargains with an imprisoned killer for insight into another one still at work."),

        new("beloved", "Beloved", "Toni Morrison", Genres.Literary, 1987, 324, 2, Demand.Steady,
            "Years after escaping slavery, a woman's house in Ohio is haunted by something that will not let her leave what she did behind."),
        new("never-let-me-go", "Never Let Me Go", "Kazuo Ishiguro", Genres.Literary, 2005, 288, 3, Demand.Popular,
            "Three friends look back on an English boarding school that was gentle with them about everything except what they were for."),
        new("the-remains-of-the-day", "The Remains of the Day", "Kazuo Ishiguro", Genres.Literary, 1989, 258, 2, Demand.Steady,
            "An English butler drives across the country in 1956 and slowly realises what his lifetime of perfect service was in service of."),
        new("klara-and-the-sun", "Klara and the Sun", "Kazuo Ishiguro", Genres.Literary, 2021, 303, 3, Demand.Steady,
            "An artificial friend watches from a shop window, learning about love and illness from a family that needs her for reasons she is not told."),
        new("normal-people", "Normal People", "Sally Rooney", Genres.Literary, 2018, 266, 4, Demand.Popular,
            "Two people from the same small Irish town keep finding and losing each other from school through university."),

        new("pride-and-prejudice", "Pride and Prejudice", "Jane Austen", Genres.Classics, 1813, 279, 3, Demand.Steady,
            "Elizabeth Bennet is quick to judge and Mr Darcy gives her every reason to, in a society where a wrong marriage is a life sentence."),
        new("nineteen-eighty-four", "Nineteen Eighty-Four", "George Orwell", Genres.Classics, 1949, 328, 4, Demand.Popular,
            "A minor official whose job is rewriting the past commits the crime of keeping a private thought."),
        new("brave-new-world", "Brave New World", "Aldous Huxley", Genres.Classics, 1932, 311, 2, Demand.Steady,
            "A society has abolished suffering by engineering the people who might have felt it, and one man cannot stop noticing the cost."),

        new("sapiens", "Sapiens", "Yuval Noah Harari", Genres.NonFiction, 2011, 443, 4, Demand.Popular,
            "A history of how an unremarkable ape came to run the planet, arguing that the decisive invention was shared fiction."),
        new("thinking-fast-and-slow", "Thinking, Fast and Slow", "Daniel Kahneman", Genres.NonFiction, 2011, 499, 3, Demand.Steady,
            "A tour of the two systems behind human judgement, and of the reliable ways the fast one gets things wrong."),
        new("the-selfish-gene", "The Selfish Gene", "Richard Dawkins", Genres.NonFiction, 1976, 360, 2, Demand.Quiet,
            "A reframing of evolution from the gene's point of view, in which bodies are vehicles built to carry replicators forward."),
        new("why-we-sleep", "Why We Sleep", "Matthew Walker", Genres.NonFiction, 2017, 368, 2, Demand.Steady,
            "A case that sleep is not downtime but the foundation of health, memory and mood, and that most of us are running a deficit.")
    ];

    /// <summary>
    /// Deliberately a lot of borrowers for a small catalogue. Recommendations are computed
    /// from who read what alongside what, and with too few readers everyone has read
    /// everything: the overlap between any two titles saturates and the ranking flattens
    /// into noise. Spreading the same volume of borrowing across more people is what gives
    /// the co-borrowing signal somewhere to show up.
    /// </summary>
    public static IReadOnlyList<DemoBorrower> Borrowers { get; } =
    [
        new("astrid.lindqvist@example.se", "Astrid Lindqvist", Genres.Crime, Genres.Literary),
        // Named for the joke, and placed first because of it: "tom" is Swedish for empty, so
        // the member with nothing in his history is Absolutely Empty. Sorts to the top of the
        // sign-in list, which is where the newcomer's view of the app wants to be.
        new("absolut.tom@example.se", "Absolut Tom", Genres.Literary, Genres.Crime, JustJoined: true),
        new("bjorn.ek@example.se", "Bjorn Ek", Genres.SciFiFantasy, Genres.NonFiction),
        new("cecilia.nordin@example.se", "Cecilia Nordin", Genres.Literary, Genres.Classics),
        new("david.holm@example.se", "David Holm", Genres.NonFiction, Genres.SciFiFantasy),
        new("elin.sandberg@example.se", "Elin Sandberg", Genres.SciFiFantasy, Genres.Classics),
        new("fredrik.ahl@example.se", "Fredrik Ahl", Genres.Crime, Genres.NonFiction),
        new("greta.lund@example.se", "Greta Lund", Genres.Literary, Genres.Crime),
        new("hassan.karimi@example.se", "Hassan Karimi", Genres.NonFiction, Genres.Classics),
        new("ingrid.falk@example.se", "Ingrid Falk", Genres.Classics, Genres.Literary),
        new("johan.berg@example.se", "Johan Berg", Genres.SciFiFantasy, Genres.Crime),
        new("karin.ohlsson@example.se", "Karin Ohlsson", Genres.Crime, Genres.Classics),
        new("lars.wikstrom@example.se", "Lars Wikstrom", Genres.NonFiction, Genres.Literary),
        new("maja.strom@example.se", "Maja Strom", Genres.Literary, Genres.SciFiFantasy),
        new("nils.agren@example.se", "Nils Agren", Genres.Classics, Genres.NonFiction),
        new("olivia.hedman@example.se", "Olivia Hedman", Genres.SciFiFantasy, Genres.Literary),
        new("petter.sundqvist@example.se", "Petter Sundqvist", Genres.Crime, Genres.SciFiFantasy),
        new("quynh.tran@example.se", "Quynh Tran", Genres.Literary, Genres.NonFiction),
        new("rebecka.moller@example.se", "Rebecka Moller", Genres.NonFiction, Genres.Crime),
        new("samuel.osei@example.se", "Samuel Osei", Genres.Classics, Genres.SciFiFantasy),
        new("tove.bergstrom@example.se", "Tove Bergstrom", Genres.SciFiFantasy, Genres.Crime),
        new("ulf.lindgren@example.se", "Ulf Lindgren", Genres.Crime, Genres.Literary),
        new("vera.nyman@example.se", "Vera Nyman", Genres.Literary, Genres.Classics),
        new("william.sjoberg@example.se", "William Sjoberg", Genres.NonFiction, Genres.SciFiFantasy),
        new("ylva.dahl@example.se", "Ylva Dahl", Genres.Classics, Genres.Crime),
        new("zaid.haddad@example.se", "Zaid Haddad", Genres.SciFiFantasy, Genres.NonFiction),
        new("agnes.forsberg@example.se", "Agnes Forsberg", Genres.Crime, Genres.Classics),
        new("bo.eriksson@example.se", "Bo Eriksson", Genres.NonFiction, Genres.Literary),
        new("clara.wallin@example.se", "Clara Wallin", Genres.Literary, Genres.SciFiFantasy),
        new("daniel.persson@example.se", "Daniel Persson", Genres.Classics, Genres.NonFiction),
        new("ebba.sandstrom@example.se", "Ebba Sandstrom", Genres.SciFiFantasy, Genres.Classics),
        new("filip.norberg@example.se", "Filip Norberg", Genres.Crime, Genres.NonFiction),
        new("gabriella.roos@example.se", "Gabriella Roos", Genres.Literary, Genres.Crime),
        new("henrik.lindahl@example.se", "Henrik Lindahl", Genres.NonFiction, Genres.Classics),
        new("iris.blomqvist@example.se", "Iris Blomqvist", Genres.Classics, Genres.Literary),
        new("jonas.hellstrom@example.se", "Jonas Hellstrom", Genres.SciFiFantasy, Genres.Literary),
        new("katarina.ohman@example.se", "Katarina Ohman", Genres.Crime, Genres.Literary),
        new("leo.andersson@example.se", "Leo Andersson", Genres.Literary, Genres.NonFiction),
        new("miriam.holmberg@example.se", "Miriam Holmberg", Genres.NonFiction, Genres.SciFiFantasy),
        new("noah.kallstrom@example.se", "Noah Kallstrom", Genres.Classics, Genres.SciFiFantasy),
        new("oskar.lindqvist@example.se", "Oskar Lindqvist", Genres.SciFiFantasy, Genres.NonFiction),
        new("paulina.grip@example.se", "Paulina Grip", Genres.Crime, Genres.Classics),
        new("rasmus.ek@example.se", "Rasmus Ek", Genres.Literary, Genres.Classics),
        new("sofia.marklund@example.se", "Sofia Marklund", Genres.NonFiction, Genres.Crime),
        new("teodor.wiberg@example.se", "Teodor Wiberg", Genres.Classics, Genres.Crime),
        new("ulrika.stenberg@example.se", "Ulrika Stenberg", Genres.SciFiFantasy, Genres.Literary)
    ];
}
