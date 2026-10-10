using Atten.Core;
using Xunit;

namespace Atten.Data.Tests;

public class PayrollRepositoryTests
{
    [Fact]
    public void DefaultUserIsTheOnlyActorUntilLoginExists()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();

        UserRecord user = Assert.Single(store.Users.List());
        Assert.Equal(DefaultUser.Id, user.Id);
        Assert.Equal(DefaultUser.DisplayName, user.DisplayName);
        Assert.Equal(user, store.Users.Get(DefaultUser.Id));
        Assert.Null(store.Users.Get(99));
    }

    [Fact]
    public void DraftReadsLeaveBalanceAndWorkTotalsThenStampsTheActor()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        var (start, end) = Payroll.MonthSpan(1405, 7);
        DateOnly leaveDay = Dates.ParseShamsiDate("1405/07/10");
        int leaveMinutes = Leave.ComposeLeaveMinutes(1, 0, 0);
        long personId = store.Personnel.Add(
            "علی",
            "رضایی",
            8,
            remoteId: "dev-1",
            cooperationStart: Dates.ParseShamsiDate("1404/01/01"));
        store.Calculation.SaveBalances(
            "dev-1",
            [
                (Dates.ParseShamsiDate("1405/07/02"), 60, false),
                (Dates.ParseShamsiDate("1405/07/03"), -30, false),
                (Dates.ParseShamsiDate("1405/07/04"), 10, true),
            ]);
        store.Leaves.Add(personId, leaveDay, leaveDay, leaveMinutes);
        PayrollRunRecord run = store.Payroll.Draft(personId, start, end);

        int remaining = Leave.RemainingLeaveMinutes(
            1405,
            Dates.ParseShamsiDate("1404/01/01"),
            null,
            new Dictionary<int, int> { [1405] = leaveMinutes });
        Assert.Equal(personId, run.PersonnelId);
        Assert.Equal(Dates.StorageDate(start), run.StartDate);
        Assert.Equal(Dates.StorageDate(end), run.EndDate);
        Assert.Equal(70, run.OvertimeMinutes);
        Assert.Equal(30, run.DeficitMinutes);
        Assert.Equal(leaveMinutes, run.LeaveMinutes);
        Assert.Equal(remaining, run.RemainingLeaveMinutes);
        Assert.Equal(Payroll.Draft, run.Status);
        Assert.Equal(DefaultUser.Id, run.CreatedBy);
        Assert.Equal(DefaultUser.Id, run.UpdatedBy);
        Assert.False(string.IsNullOrWhiteSpace(run.CreatedAt));
        Assert.Equal(run.CreatedAt, run.UpdatedAt);
        Assert.Equal(run, store.Payroll.Get(run.Id));
        Assert.Equal([run], store.Payroll.List(personId));
    }

    [Fact]
    public void DraftUpdatesTheSamePeriodAndConfirmLocksItForTheSameUser()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        var (start, end) = Payroll.MonthSpan(1405, 7);
        long personId = store.Personnel.Add("علی", "رضایی", 8, remoteId: "dev-1");
        store.Calculation.SaveBalances("dev-1", [(start, 15, false)]);
        PayrollRunRecord first = store.Payroll.Draft(personId, start, end);

        store.Calculation.SaveBalances("dev-1", [(start, 45, false), (start.AddDays(1), -20, false)]);
        PayrollRunRecord again = store.Payroll.Draft(personId, start, end);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.CreatedAt, again.CreatedAt);
        Assert.Equal(45, again.OvertimeMinutes);
        Assert.Equal(20, again.DeficitMinutes);
        Assert.Equal(DefaultUser.Id, again.CreatedBy);
        Assert.Equal(DefaultUser.Id, again.UpdatedBy);

        store.Payroll.Confirm(again.Id);
        PayrollRunRecord confirmed = store.Payroll.Get(again.Id)!;
        Assert.Equal(Payroll.Confirmed, confirmed.Status);
        Assert.Equal(DefaultUser.Id, confirmed.UpdatedBy);

        ArgumentException locked = Assert.Throws<ArgumentException>(() => store.Payroll.Draft(personId, start, end));
        Assert.Equal("حقوق تأییدشده را نمی‌توان دوباره محاسبه کرد.", locked.Message);
        Assert.Throws<ArgumentException>(() => store.Payroll.Recalculate(again.Id));

        store.Payroll.Reopen(again.Id);
        store.Payroll.Recalculate(again.Id);
        PayrollRunRecord reopened = store.Payroll.Get(again.Id)!;
        Assert.Equal(Payroll.Draft, reopened.Status);
        Assert.Equal(45, reopened.OvertimeMinutes);
        Assert.Equal(DefaultUser.Id, reopened.UpdatedBy);
    }

    [Fact]
    public void DraftWithoutRemoteIdStillReadsLeaveAndConfirmNeedsADraft()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        var (start, end) = Payroll.MonthSpan(1405, 1);
        DateOnly day = Dates.ParseShamsiDate("1405/01/01");
        long personId = store.Personnel.Add(
            "مریم",
            "احمدی",
            8,
            cooperationStart: day,
            cooperationEnd: day);
        store.Leaves.Add(personId, day, day, 35);

        PayrollRunRecord run = store.Payroll.Draft(personId, start, end);
        Assert.Equal(0, run.OvertimeMinutes);
        Assert.Equal(0, run.DeficitMinutes);
        Assert.Equal(35, run.LeaveMinutes);
        Assert.Equal(0, run.RemainingLeaveMinutes);

        ArgumentException missing = Assert.Throws<ArgumentException>(() => store.Payroll.Confirm(99));
        Assert.Equal("این حقوق دیگر وجود ندارد.", missing.Message);
        store.Payroll.Confirm(run.Id);
        ArgumentException again = Assert.Throws<ArgumentException>(() => store.Payroll.Confirm(run.Id));
        Assert.Equal("فقط پیش‌نویس را می‌توان تأیید کرد.", again.Message);
        store.Payroll.Reopen(run.Id);
        ArgumentException notConfirmed = Assert.Throws<ArgumentException>(() => store.Payroll.Reopen(run.Id));
        Assert.Equal("فقط حقوق تأییدشده را می‌توان به پیش‌نویس برگرداند.", notConfirmed.Message);
    }
}
