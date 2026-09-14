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


-- Payment columns used by the Stripe checkout path in CheckoutController but
-- absent from the EF migration, so the INSERT/SELECT there would fail.
IF COL_LENGTH('Payment', 'Currency') IS NULL
    ALTER TABLE Payment ADD Currency nvarchar(10) NULL;
IF COL_LENGTH('Payment', 'Provider') IS NULL
    ALTER TABLE Payment ADD Provider nvarchar(20) NULL;
IF COL_LENGTH('Payment', 'Provider_payment_id') IS NULL
    ALTER TABLE Payment ADD Provider_payment_id nvarchar(255) NULL;
GO
