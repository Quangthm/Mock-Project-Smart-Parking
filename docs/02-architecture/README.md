# Architecture

Start with [Solution Architecture v1.0](solution-architecture/smartpark-solution-architecture-v1.0.md) for the current project-wide direction, then read the detailed contracts below. Product behavior is summarized in [Consolidated SRS v1.0](../01-requirements/srs/smartpark-srs-v1.0.md).

The approved RC3 target uses six domain-owned services, Clean Architecture, PostgreSQL boundaries, HTTP/OpenAPI for immediate requests and **RabbitMQ + MassTransit** for durable asynchronous outcomes. Kafka is deferred. Search is an Operations module; Assistant is an IoT & Recognition module; General Support is a Notification & Audit module.

- [Service ownership and event contracts](service-boundaries/ownership-and-events.md)
- [Media lifecycle and portability](service-boundaries/media-contract.md)
- [payOS integration](payment-integration/payos.md)
- [Admin recovery runbook](service-boundaries/admin-recovery-runbook.md)
- [Database alignment backlog](database/alignment-backlog.md)
- [API documents](api/README.md)
- [Architecture decision workspace](architecture-decisions/README.md)

[Solution Architecture v0.9](solution-architecture/SmartPark_Solution_Architecture_v0.9.md) is retained as a historical reference. Its earlier Kafka, service-split, security, payment and quality assumptions must not override current requirements or the linked v1.0 contracts. Existing schemas/API designs require alignment rather than automatic promotion to business authority.

This is a target architecture and review candidate, not evidence of completed implementation or passed acceptance.
