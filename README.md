# DocumentDispatchService

A distributed document dispatch simulation service built with ASP.NET
Core, EF Core, and a background worker.

This project demonstrates lease-based worker coordination, configurable
concurrency, retry handling, and real-time operational visibility
through a Razor-based Ops dashboard.

## Overview

DocumentDispatchService simulates a document delivery pipeline where:

-   Dispatch requests are created and stored in a database
-   A background worker claims jobs using a lease-based locking model
-   Jobs are processed concurrently
-   Retries and failures are handled deterministically
-   Operational state is observable in real time

The goal of this project is to demonstrate backend system design
concepts commonly used in production distributed systems.

## Architectural Concepts Demonstrated

-   Lease-based job claiming with lock ownership
-   Optimistic concurrency via atomic update conditions
-   Configurable worker concurrency
-   Retry and requeue behavior
-   Failure thresholds
-   In-flight tracking
-   Prometheus metrics exposure
-   Real-time operational dashboard
-   Config-driven runtime behavior
-   Activity stream instrumentation

## Operations Dashboard

Accessible at:

http://localhost:5250/ops

Features:

-   Live snapshot metrics
-   Adjustable recent dispatch view from 15 to 100 rows
-   Toggle to show or hide completed items
-   Auto-refresh with visible countdown
-   Scrollable real-time table
-   Activity feed showing worker events
-   Demonstration job generation controls

The dashboard is designed to resemble an internal operations console.

## Background Worker

The DispatchWorker:

-   Claims jobs in batches
-   Uses lease ownership fields LockOwner and LockedUntilUtc
-   Renews leases periodically
-   Processes jobs concurrently
-   Simulates success and failure
-   Requeues transient failures
-   Permanently fails after retry threshold

Worker behavior is configurable via appsettings.json.

## Configuration

Example configuration:

{ "DispatchWorker": { "PollDelayMs": 500, "BatchSize": 10,
"MaxConcurrency": 3, "LeaseSeconds": 30, "LeaseRenewEverySeconds": 10,
"WorkDelayMs": 300 } }

Key settings:

-   PollDelayMs: Delay between polling cycles
-   BatchSize: Number of jobs claimed per tick
-   MaxConcurrency: Concurrent processing limit
-   LeaseSeconds: Lease duration per job
-   LeaseRenewEverySeconds: Lease renewal interval
-   WorkDelayMs: Simulated processing duration

## Metrics

Prometheus metrics are exposed at:

/metrics

Includes:

-   Dispatch claimed total
-   Dispatch processed counts including completed, failed, requeued, and
    lease lost
-   In-flight job count
-   Processing duration histogram
-   Lease renewal counts
-   Worker error counts

## Demo Controls

The Ops dashboard includes demonstration controls for generating test
loads:

-   Create 1 to 50 jobs
-   Live worker-driven mode
-   Static mixed status mode
-   Failed-only mode
-   Clear all jobs with confirmation

This enables visual demonstration of worker coordination behavior.

## Tech Stack

-   ASP.NET Core
-   Razor Pages
-   Entity Framework Core
-   BackgroundService
-   Prometheus-net
-   In-memory activity stream
-   Config-driven runtime tuning

## Purpose

This project exists to demonstrate:

-   Backend system design
-   Distributed processing concepts
-   Concurrency management
-   Observability practices
-   Operational user interface design
-   Clean feature branching and incremental evolution

It is intended as a portfolio and recruitment-ready backend
demonstration.

## Running Locally

1.  Clone the repository
2.  Configure the database connection
3.  Apply EF migrations if applicable
4.  Run the application
5.  Navigate to /ops

## Author

Nevin Kadlec Software Developer focused on distributed systems,
operational tooling, and scalable backend architecture.
