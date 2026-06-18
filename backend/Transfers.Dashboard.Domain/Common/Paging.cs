namespace Transfers.Dashboard.Domain.Common;

/// <summary>Base paging request with sane clamping helpers.</summary>
public abstract class PagedQuery
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    protected virtual int MaxPageSize => 100;

    public int Skip => (Page - 1) * PageSize;
}
