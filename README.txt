Version 1: 04-08-2025

This codebase is s PoC seachengine that consist of two programs and a class library.

The two programs are the indexer (also called a crawler) and a search program. Both
are simple console programs.

The indexer will crawl a folder (in depth) and create a reverse index
in a database. It will only index text files with .txt as extension.

The search program (see the ConsoleSearch project) offers a query-based search
in the reverse index.

The class library Shared contains classes that are used by the indexer
and the ConsoleSearch. It contains:

- Paths containing a static path the database (used by both the indexer (write-only), and
the search program (read-only).
- BEDocument (BE for Business Entity) - a class representing a document.



