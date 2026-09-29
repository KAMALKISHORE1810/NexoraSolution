-- Optional reference schema. The application normally creates this via EF Core EnsureCreated.
CREATE DATABASE NexoraHRMS;
GO
USE NexoraHRMS;
GO
-- For the authoritative schema, run the EF Core migration commands in README after restoring packages.


-- Added for Manager performance/appraisal history tracking
IF COL_LENGTH('Evaluations','EvaluatedByEmployeeId') IS NULL ALTER TABLE Evaluations ADD EvaluatedByEmployeeId int NULL;
IF COL_LENGTH('Appraisals','AppraisedByEmployeeId') IS NULL ALTER TABLE Appraisals ADD AppraisedByEmployeeId int NULL;

-- Additional compatibility column for payroll payment audit date
IF COL_LENGTH('Payrolls','PaymentDate') IS NULL ALTER TABLE Payrolls ADD PaymentDate datetime2 NULL;
