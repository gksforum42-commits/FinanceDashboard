using Microsoft.AspNetCore.Mvc.Rendering;

namespace FinanceDashboard.ViewModels
{
    public class FilterViewModel
    {
        public int? FinancialYear { get; set; }
        public string? FundCode { get; set; }
        public string? CostCenterCode { get; set; }
        public string? DepartmentCode { get; set; }
        public string? Grant { get; set; }
        public string? FunctionCode { get; set; }
        public string? ObjectCode { get; set; }
        public string? MajorHead { get; set; }
        public string? MinorHead { get; set; }
        public string? Scheme { get; set; }
        public string? Keyword { get; set; }
        public string? Sector { get; set; }

        // Multi-value filters for Cost Center Wise export
        public List<string> CostCenterCodes { get; set; } = new();
        public List<string> ObjectCodes     { get; set; } = new();

        // Grouping level flags for Object Wise export
        public bool ShowSector { get; set; } = true;
        public bool ShowDept   { get; set; } = true;
        public bool ShowFund   { get; set; } = true;
        public bool ShowCC     { get; set; } = true;

        // Lookup lists for dropdowns
        public List<SelectListItem> FinancialYears { get; set; } = new();
        public List<SelectListItem> Funds { get; set; } = new();
        public List<SelectListItem> CostCenters { get; set; } = new();
        public List<SelectListItem> Departments { get; set; } = new();
        public List<SelectListItem> Functions { get; set; } = new();
    }

    public class PaginationViewModel
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public string? SortColumn { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }

    public class PagedResultViewModel<T>
    {
        public List<T> Data { get; set; } = new();
        public PaginationViewModel Pagination { get; set; } = new();
        public FilterViewModel? Filters { get; set; }
    }

    public class ImportViewModel
    {
        public IFormFile? ExcelFile { get; set; }
        public int FinancialYear { get; set; } = DateTime.Now.Year;
        public string ImportType { get; set; } = "Both";
        public bool ReplaceExisting { get; set; } = false;
    }

    public class ImportPreviewViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public int ValidRecords { get; set; }
        public int InvalidRecords { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<Dictionary<string, object>> PreviewData { get; set; } = new();
        public int FinancialYear { get; set; }
        public string ImportType { get; set; } = "Both";
    }

    public class ApiResponseViewModel<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class DataTableRequestViewModel
    {
        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public string? SearchValue { get; set; }
        public string? SortColumn { get; set; }
        public string? SortDirection { get; set; }
        public int? FinancialYear { get; set; }
        public string? FundCode { get; set; }
        public string? CostCenterCode { get; set; }
        public string? DepartmentCode { get; set; }
        public string? Keyword { get; set; }
    }

    public class DataTableResponseViewModel<T>
    {
        public int Draw { get; set; }
        public int RecordsTotal { get; set; }
        public int RecordsFiltered { get; set; }
        public List<T> Data { get; set; } = new();
    }
}
