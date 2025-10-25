# CODEBUDDY.md This file provides guidance to CodeBuddy Code when working with code in this repository.

## Project Overview

DataEditor is a C# WPF desktop application for editing and managing game data. It supports loading/saving data, CSV import/export, and follows the MVVM pattern.

## Build and Development Commands

- **Build**: `dotnet build`
- **Run**: `dotnet run`
- **Clean**: `dotnet clean`
- **Target Framework**: .NET 7.0 Windows

## Architecture

### Core Architecture (MVVM)

- **Models**: Data structures and business logic
  - `GameDataTable`: Represents a table of game data entries
  - `BaseDataRow` and specific data entry types (Item, Monster, Quest)
  - `AppSettings`: Application configuration
- **ViewModels**: Business logic and data binding
  - `MainViewModel`: Main application logic for data management
  - `SettingsViewModel`: Settings management logic
  - `FieldViewModel`: Individual field editing logic
- **Views**: XAML UI definitions
  - `MainWindow.xaml`: Primary application interface
  - `SettingsWindow.xaml`: Application settings interface

### Key Data Structures

- **GameDataTable**: Contains collections of `BaseDataRow` objects
- **Data Entries**: Typed data models in `Models/DataEntries/`
  - `Item`, `Monster`, `Quest` - specific game data types
  - Complex types in `Complex/` subdirectory (Aura, Stats, Resistances)
- **Table Mapping**: `TableTypeMapping` in MainViewModel maps table names to data types

### Services and Utilities

- **SettingsService**: Manages application settings persistence
- **CsvService**: Handles CSV import/export functionality
- **RelayCommand**: Command pattern implementation for MVVM

### UI Layout

The application uses a three-column layout:
1. Left: List of data tables (GameDataTable)
2. Middle: List of rows for selected table (GameDataRow)
3. Right: Key-value display of fields for selected row

## Data Persistence

- Data is stored as JSON files in folders
- Each table corresponds to a collection of JSON files
- Settings are persisted using `SettingsService`

## Important Implementation Notes

- Uses `ObservableCollection<T>` for data binding with UI updates
- Implements `INotifyPropertyChanged` for property change notifications
- Foreign key relationships are supported via `ForeignKey` model
- CSV export preserves field order using `OrderedPropertiesResolver`

## Common Development Tasks

- Adding new data types: Create classes in `Models/DataEntries/` and update `TableTypeMapping`
- Modifying UI layout: Edit XAML files in root directory
- Adding new functionality: Extend existing ViewModels or create new ones
- Settings management: Use `SettingsService` and `AppSettings` model