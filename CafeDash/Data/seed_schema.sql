-- Structural fixes applied on every startup. Must stay idempotent.
--
-- Chats is queried by raw SQL in HomeController/AdminController but was never
-- created by the EF migration, so it is created here if missing.
IF OBJECT_ID('Chats') IS NULL
BEGIN
    CREATE TABLE Chats (
        Message_ID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        User_ID int NOT NULL,
        Sender_Type varchar(20) NOT NULL,
        Message_Text varchar(2000) NOT NULL,
        Sent_at datetime NOT NULL DEFAULT GETDATE()
    );
END
GO

