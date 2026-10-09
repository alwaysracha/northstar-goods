/*
    procedures.sql
    Stored procedures, re-applied on every deploy (CREATE OR ALTER).

    Only one procedure is needed. Plain CRUD stays in the application (EF Core). A
    procedure is used where the database itself must guarantee a multi-row rule
    atomically, whichever client makes the change.
*/
SET NOCOUNT ON;
GO

/*
    sales.usp_ChangeOrderStatus
    Changes an order's status and writes its audit row in one transaction.

    - The transition must exist in ref.OrderStatusTransitions.
    - Optimistic concurrency: the change only applies if the order is still in
      @ExpectedStatusId (the status the admin was looking at).
    - The history row and the status update commit together or not at all.

    @Result: 0 = changed, 1 = order not found, 2 = order changed since it was read,
             3 = transition not allowed
*/
CREATE OR ALTER PROCEDURE sales.usp_ChangeOrderStatus
    @OrderId              int,
    @ExpectedStatusId     tinyint,
    @NewStatusId          tinyint,
    @ChangedByUserId      nvarchar(64) = NULL,
    @StatusChangeReasonId smallint     = 10, -- ADMIN_UPDATE
    @Note                 nvarchar(500) = NULL,
    @Result               int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM ref.OrderStatusTransitions WHERE FromStatusId = @ExpectedStatusId AND ToStatusId = @NewStatusId)
    BEGIN
        SET @Result = 3;
        RETURN;
    END

    BEGIN TRANSACTION;

    UPDATE sales.Orders
    SET OrderStatusId = @NewStatusId
    WHERE Id = @OrderId AND OrderStatusId = @ExpectedStatusId;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        SET @Result = CASE WHEN EXISTS (SELECT 1 FROM sales.Orders WHERE Id = @OrderId) THEN 2 ELSE 1 END;
        RETURN;
    END

    INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, Note)
    VALUES (@OrderId, @ExpectedStatusId, @NewStatusId, @StatusChangeReasonId, @ChangedByUserId, @Note);

    COMMIT TRANSACTION;
    SET @Result = 0;
END
GO
