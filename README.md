# QInfoRanker

キーワードベース情報アグリゲーター＆ランキングツール

## Overview

QInfoRanker is a keyword-based information aggregation and ranking tool that collects articles from multiple sources and ranks them using hybrid scoring (native scores + LLM evaluation).

**Initial keyword**: "量子コンピュータ" (Quantum Computing)

## Features

- **Keyword Management**: Add and manage search keywords for article collection
- **Source Management**: Configure multiple information sources (API, RSS, Web Scraping)
- **Article Collection**: Automatic weekly collection from configured sources
- **Hybrid Scoring**: Combines native scores (likes, bookmarks) with LLM-based quality evaluation
- **Ranking View**: View top-ranked articles by time period (day/week/month)
- **Template Sources**: Pre-configured sources including:
  - はてなブックマーク (Hatena Bookmark)
  - Qiita
  - Zenn
  - arXiv
  - Hacker News
  - Reddit

## Tech Stack

- **Frontend/Backend**: .NET 8 Blazor Web App (Server/WASM Hybrid)
- **Database**: SQLite (Development), Azure SQL Database (Production)
- **ORM**: Entity Framework Core 8
- **LLM**: Azure OpenAI (planned)
- **Deployment**: Azure App Service (planned)
- **Scheduler**: Azure Functions / Hangfire (planned)

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later

### Setup and Run

1. Clone the repository:
```bash
git clone https://github.com/binnmti/QInfoRanker.git
cd QInfoRanker
```

2. Build the solution:
```bash
dotnet build
```

3. Apply database migrations:
```bash
cd src/QInfoRanker.Infrastructure
dotnet ef database update --startup-project ../QInfoRanker.Web/QInfoRanker.Web
```

4. Run the application:
```bash
cd ../QInfoRanker.Web/QInfoRanker.Web
dotnet run
```

5. Open your browser and navigate to the URL shown in the console (typically `http://localhost:5xxx`)

## Project Structure

```
QInfoRanker/
├── src/
│   ├── QInfoRanker.Core/           # Domain models and interfaces
│   │   ├── Entities/               # Keyword, Source, Article
│   │   └── Interfaces/             # ICollector, IScoringService, etc.
│   ├── QInfoRanker.Infrastructure/ # Data access and external services
│   │   ├── Data/                   # DbContext and migrations
│   │   ├── Collectors/             # Article collectors (planned)
│   │   └── Scoring/                # LLM scoring service (planned)
│   └── QInfoRanker.Web/           # Blazor Web App
│       ├── QInfoRanker.Web/       # Server-side components
│       └── QInfoRanker.Web.Client/# WebAssembly components
└── tests/
    └── QInfoRanker.Tests/         # Unit tests
```

## Data Model

### Keyword
- Term: Search keyword (e.g., "量子コンピュータ")
- IsActive: Whether active for collection
- Sources: Associated information sources

### Source
- Name: Source name (e.g., "Qiita", "arXiv")
- URL: Base URL
- SearchUrlTemplate: URL template with {keyword} placeholder
- Type: API, RSS, or Scraping
- HasNativeScore: Whether source provides scoring
- AuthorityWeight: Source authority (0.0-1.0)
- IsAutoDiscovered: Whether discovered by LLM

### Article
- Title, URL, Summary
- PublishedAt, CollectedAt
- NativeScore: Original score from source
- LlmScore: LLM evaluation score (0-100)
- FinalScore: Hybrid final score

## Scoring Logic

```
Final Score = (Normalized Native Score × Native Weight) + (LLM Score × LLM Weight) + (Authority Bonus)
```

- **With Native Score** (Hatena, Qiita): Native Weight = 0.7, LLM Weight = 0.3
- **Without Native Score** (arXiv, custom blogs): LLM Weight = 1.0 + Authority Bonus

## Roadmap

- [x] Phase 1: Project structure and domain models
- [x] Phase 2: Database setup with EF Core
- [x] Phase 3: Basic Blazor UI (Keywords, Sources, Articles, Ranking)
- [ ] Phase 4: Article collection implementation
- [ ] Phase 5: Azure OpenAI integration for scoring
- [ ] Phase 6: LLM-powered source recommendation
- [ ] Phase 7: Background job scheduling
- [ ] Phase 8: Azure deployment

## License

This project is open source and available under the [MIT License](LICENSE).
