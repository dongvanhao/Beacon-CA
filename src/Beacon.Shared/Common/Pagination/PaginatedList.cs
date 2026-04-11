using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Shared.Common.Pagination
{
    
        public class PaginatedList<T>
        {
            public List<T> Items { get; }
            public int TotalCount { get; }
            public int Page { get; }
            public int PageSize { get; }

            public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
            public bool HasNextPage => Page < TotalPages;
            public bool HasPreviousPage => Page > 1;

            public PaginatedList(List<T> items, int totalCount, int page, int pageSize)
            {
                Items = items;
                TotalCount = totalCount;
                Page = page;
                PageSize = pageSize;
            }

            public static async Task<PaginatedList<T>> CreateAsync(
                IQueryable<T> source, int page, int pageSize,
                CancellationToken ct = default)
            {
                var totalCount = await source.CountAsync(ct);
                var items = await source
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                return new PaginatedList<T>(items, totalCount, page, pageSize);
            }
        }
}
