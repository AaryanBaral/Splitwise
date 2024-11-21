## Table of Contents
1. [Features](#features)
2. [Technologies Used]
3. [Getting Started]
4. [Project Structure]
5. [Endpoints]
6. [Database Schema]
7. [Usage]
8. [Future Enhancements]
9. [Contributing]
10. [License]

## Features
- User Management: Sign up, log in, and manage user profiles.
- Expense Management: Add, update, and delete expenses.
- Group Management: Create and manage groups for splitting expenses.
- Settlement: Calculate and settle balances among group members.
- Real-Time Calculations: Recalculate balances efficiently using optimized logic.
- Multi-Payer Support: Split expenses across multiple payers.
## Technologies Used
- Backend Framework: .NET Core
- Database: SQL Server
- Authentication: JWT (JSON Web Tokens)
- ORM: Entity Framework Core
- Tools:
  - MediatR for CQRS pattern and handling dependencies.
  - AutoMapper for mapping DTOs and entities.
  - FluentValidation for input validation.
    Getting Started
## Prerequisites
Ensure you have the following installed:
- .NET Core SDK (v6.0 or later)
- SQL Server
- Visual Studio / VS Code
- Docker (optional for containerization)
### Installation
1. Clone the repo:
   ```bash
   git clone https://github.com/username/repository.git
   cd repository
2. Restore dependencies:
   ```bash
   dotnet restore   
3. Apply database migrations:
   ```bash
   dotnet ef database update
4. Apply database migrations:
   ```bash
    dotnet run

## Usage
### Add an Expense
Send a `POST` request to `/api/expenses`:
```json
{
  "groupId": 1,
  "amount": 100,
  "description": "Lunch",
  "sharedMembers": [
    { "userId": 2, "share": 50 },
    { "userId": 3, "share": 50 }
  ]
}


