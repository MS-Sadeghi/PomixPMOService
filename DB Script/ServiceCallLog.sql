/*
  لاگ فراخوانی سرویس‌های Pomix (گزارش تردد، داشبورد، ...)
  هر فراخوانی قبل از ارسال با Stage = 'Sent' ثبت و بعد از پایان به‌روزرسانی می‌شود:
    Sent      : درخواست ارسال شده و هنوز پاسخی نرسیده (اگر ماندگار شود یعنی سرویس گیر کرده است)
    Completed : پاسخ موفق (200) دریافت شد
    Failed    : پاسخ خطا از Pomix یا خطای شبکه/پردازش
    TimedOut  : در زمان مقرر پاسخی نرسید
    Abandoned : Sent که بیش از چند دقیقه بدون پاسخ مانده و job نگهداری آن را بسته است
  (ایندکس‌های StartedAt و ServiceName+StartedAt برای صفحه‌بندی و فیلتر تاریخ از قبل وجود دارند.)
  این اسکریپت چندبار اجرا شدنی است.
*/
IF SCHEMA_ID('Log') IS NULL EXEC('CREATE SCHEMA [Log]');
GO

IF OBJECT_ID('Log.ServiceCallLog', 'U') IS NULL
BEGIN
    CREATE TABLE [Log].[ServiceCallLog](
        [LogId]            BIGINT IDENTITY(1,1) NOT NULL,
        [ServiceName]      NVARCHAR(100)  NOT NULL,
        [Endpoint]         NVARCHAR(300)  NULL,
        [RequestParams]    NVARCHAR(2000) NULL,
        [UserId]           BIGINT         NULL,
        [Username]         NVARCHAR(100)  NULL,
        [IpAddress]        VARCHAR(45)    NULL,
        [TraceId]          VARCHAR(64)    NULL,
        [StartedAt]        DATETIME2(3)   NOT NULL,
        [CompletedAt]      DATETIME2(3)   NULL,
        [DurationMs]       INT            NULL,
        [Stage]            VARCHAR(20)    NOT NULL,
        [HttpStatusCode]   INT            NULL,
        [PomixStatusCode]  INT            NULL,
        [ResponseSize]     INT            NULL,
        [ErrorMessage]     NVARCHAR(1000) NULL,
        CONSTRAINT [PK_ServiceCallLog] PRIMARY KEY CLUSTERED ([LogId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_ServiceCallLog_StartedAt]
        ON [Log].[ServiceCallLog]([StartedAt] DESC);

    CREATE NONCLUSTERED INDEX [IX_ServiceCallLog_Service_StartedAt]
        ON [Log].[ServiceCallLog]([ServiceName], [StartedAt] DESC);
END
GO
