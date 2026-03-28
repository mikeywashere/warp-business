# Session: Time Tracking Plugin
**Date:** 2026-03-28
**Requested by:** Michael R. Schmidt
**Branch:** feature/time-tracking-plugin

## Request
Add a time tracking plugin for employees. Key requirements:
- Track employee time for the company (internal work)
- Track employee time on customer sites (billable work)
- Customer billing rates per employee × per company
- Employee pay rates (hourly, daily, monthly, yearly)
- Time entry status workflow: Draft → Submitted → Approved → Rejected

## Design Decisions
- New plugin: WarpBusiness.Plugin.TimeTracking (schema: "timetracking")
- 4 domain entities: TimeEntryType, EmployeePayRate, CustomerBillingRate, TimeEntry
- Loose coupling to Employee/Company (GUID + denormalized names, no cross-schema FKs)
- Billing rates are hourly; pay rates support hourly/daily/monthly/yearly
- Follows exact same plugin pattern as Catalog (CatalogModule, CatalogDbContext, etc.)

## Team
- 🔧 Hicks — building entire plugin (domain, data, services, controllers, module, wiring)
- 🧪 Hudson — integration tests (after Hicks completes)
- 📋 Scribe — session logging
