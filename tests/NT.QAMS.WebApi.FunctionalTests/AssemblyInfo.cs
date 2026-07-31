using Xunit;

// These tests configure their host through PROCESS-GLOBAL environment variables
// (see QamsWebAppFactory: with minimal hosting that is the only source that
// reliably overrides appsettings). Two factories now exist — one on the
// in-memory provider, one on real PostgreSQL — and in parallel they raced to set
// the same variables, so whichever constructed last decided the other's
// connection string and JWT secret. Running collections serially makes each
// factory's settings current when its own host is built.
//
// Cost is a few seconds on a suite that already takes ~20s; correctness of the
// VER-001 real-database tests depends on it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
