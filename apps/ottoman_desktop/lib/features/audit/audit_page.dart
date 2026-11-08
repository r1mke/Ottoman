import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Admin: who changed what and when. Read-only; entries cannot be edited or removed from here or anywhere else.
class AuditPage extends StatefulWidget {
  const AuditPage({super.key, required this.api});

  final AuditApi api;

  @override
  State<AuditPage> createState() => _AuditPageState();
}

class _AuditPageState extends State<AuditPage> {
  static const _pageSize = 30;

  final _search = TextEditingController();
  String _query = '';
  int _page = 1;
  int _version = 0;

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  void _reload({bool firstPage = false}) => setState(() {
    if (firstPage) _page = 1;
    _version++;
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(36, 0, 36, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 420,
            child: TextField(
              key: const Key('audit_search'),
              controller: _search,
              decoration: InputDecoration(
                hintText: t.auditSearchHint,
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _query.isEmpty
                    ? null
                    : IconButton(
                        key: const Key('audit_search_clear'),
                        icon: const Icon(Icons.close),
                        onPressed: () {
                          _search.clear();
                          _query = '';
                          _reload(firstPage: true);
                        },
                      ),
              ),
              textInputAction: TextInputAction.search,
              onSubmitted: (v) {
                _query = v.trim();
                _reload(firstPage: true);
              },
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: AsyncView<Paged<AuditEntry>>(
              key: ValueKey('$_version-$_query-$_page'),
              load: () => widget.api.list(
                search: _query,
                page: _page,
                pageSize: _pageSize,
              ),
              skeleton: const ListSkeleton(),
              isEmpty: (p) => p.items.isEmpty,
              emptyMessage: t.noData,
              builder: (context, page, _) {
                final pages = (page.totalCount / _pageSize).ceil().clamp(
                  1,
                  1 << 20,
                );
                return Column(
                  children: [
                    Expanded(
                      child: Card(
                        clipBehavior: Clip.antiAlias,
                        child: SizedBox(
                          width: double.infinity,
                          child: SingleChildScrollView(
                            child: DataTable(
                              key: const Key('audit_table'),
                              columns: [
                                DataColumn(label: Text(t.auditWhen)),
                                DataColumn(label: Text(t.auditWho)),
                                DataColumn(label: Text(t.auditWhat)),
                                DataColumn(label: Text(t.auditTarget)),
                              ],
                              rows: [
                                for (final e in page.items)
                                  DataRow(
                                    cells: [
                                      DataCell(Text(formatWhen(e.atUtc))),
                                      DataCell(
                                        Text(
                                          '${e.userName} (${e.role})',
                                          key: Key('audit_who_${e.id}'),
                                        ),
                                      ),
                                      DataCell(
                                        Text(
                                          '${auditAreaLabel(e.controller)} · ${e.verb}',
                                          key: Key('audit_what_${e.id}'),
                                          style: const TextStyle(
                                            fontWeight: FontWeight.w700,
                                          ),
                                        ),
                                      ),
                                      DataCell(
                                        Text(
                                          e.targetLabel ?? '-',
                                          key: Key('audit_target_${e.id}'),
                                        ),
                                      ),
                                    ],
                                  ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        Text('${page.totalCount}  ·  $_page / $pages'),
                        IconButton(
                          key: const Key('audit_prev'),
                          tooltip: t.previousPage,
                          onPressed: _page > 1
                              ? () => setState(() => _page--)
                              : null,
                          icon: const Icon(Icons.chevron_left),
                        ),
                        IconButton(
                          key: const Key('audit_next'),
                          tooltip: t.nextPage,
                          onPressed: _page < pages
                              ? () => setState(() => _page++)
                              : null,
                          icon: const Icon(Icons.chevron_right),
                        ),
                      ],
                    ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
