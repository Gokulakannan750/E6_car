# Plan: Change Staff IDs from GUID to Integer

## Context
Staff was recently added to the codebase. Its `Id` (inherited from `BaseEntity`) is currently `Guid`. For a simple lookup table with ~10-20 workers, auto-generated integers are cleaner for data entry, display, and manual SQL work.

## Changes (8 files)

### 1. Entity: `src/E6CarSpa.Domain/Entities/Staff.cs`
- Override inherited `Id` to `int` with `[Key]` + `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]`
- Keep `FullName`, `IsActive`, `Advances`, `Salaries`

### 2. DTOs: `src/E6CarSpa.Contracts/SettingsDtos.cs`
- `StaffDto`: `Guid Id` → `int Id`
- `StaffAdvanceDto`: `Guid StaffId` → `int StaffId`, rename `WorkerName` → `StaffName`
- `StaffSalaryDto`: `Guid StaffId` → `int StaffId` (already has `StaffName`)
- `StaffAdvanceSummaryDto`: `Guid StaffId` → `int StaffId`, `WorkerName` → `StaffName`
- `StaffSalarySummaryDto`: `Guid StaffId` → `int StaffId`
- `SaveStaffAdvanceRequest`: `Guid StaffId` → `int StaffId`
- `SaveStaffSalaryRequest`: `Guid StaffId` → `int StaffId`

### 3. Controllers (2 files)
- `StaffAdvancesController.cs`: Route `{id:guid}` → `{id:int}`, `Guid` params → `int`
- `StaffSalariesController.cs`: `Guid? staffId` → `int?`, `Guid.Empty` check → `<= 0`

### 4. DbContext + Migration: `AppDbContext.cs` + new migration
- Staff entity config: remove Guid assumption, add int identity
- New migration: alter Staff.Id column type, update FK columns

### 5. Client (2 files)
- `IApiClient.cs`: UpdateStaff/DeleteStaff/RestoreStaff params `Guid` → `int`
- `ApiClient.cs`: Same implementations

### 6. Desktop ViewModels (2 files)
- `StaffAdvancesViewModel.cs`: `staff.Id` references auto-resolve
- `StaffSalariesViewModel.cs`: Same

### 7. Mobile: `AdvancesPage.xaml.cs`
- `staff.Id` → int (auto-resolves through DTO)

### 8. XAML: `StaffAdvancesView.xaml`
- Summary column: `WorkerName` → `StaffName` on totals grid

## Execution Order
DTOs → Entities → Controllers → Client → ViewModels → XAML/Mobile → `dotnet ef migrations add` → build → deploy → verify
