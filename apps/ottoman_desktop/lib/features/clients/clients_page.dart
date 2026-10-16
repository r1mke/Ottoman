import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'client_detail_dialog.dart';

/// Admin: everyone who has an account. Search, open one to see what they have booked and bought, block abusers.
class ClientsPage extends StatefulWidget {
  const ClientsPage({super.key, required this.api});

  final ClientsApi api;

  @override
  State<ClientsPage> createState() => _ClientsPageState();
}

class _ClientsPageState extends State<ClientsPage> {
  static const _pageSize = 20;

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

  Future<void> _open(ClientItem client) async {
    await showDialog<void>(
      context: context,
      builder: (_) => ClientDetailDialog(api: widget.api, clientId: client.id),
    );
    // Blocking happens inside the dialog, so the list may have changed even when it was only closed.
    if (mounted) _reload();
  }

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
              key: const Key('client_search'),
              controller: _search,
              decoration: InputDecoration(
                hintText: t.clientSearchHint,
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _query.isEmpty
                    ? null
                    : IconButton(
                        key: const Key('client_search_clear'),
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
            child: AsyncView<Paged<ClientItem>>(
              key: ValueKey('$_version-$_query-$_page'),
              load: () => widget.api.list(
                search: _query,
                page: _page,
                pageSize: _pageSize,
              ),
              skeleton: const ListSkeleton(),
              isEmpty: (p) => p.items.isEmpty,
              emptyMessage: t.noData,
              builder: (context, page, _) => _ClientTable(
                page: page,
                pageNumber: _page,
                pageSize: _pageSize,
                onOpen: _open,
                onPage: (p) => setState(() => _page = p),
                t: t,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _ClientTable extends StatelessWidget {
  const _ClientTable({
    required this.page,
    required this.pageNumber,
    required this.pageSize,
    required this.onOpen,
    required this.onPage,
    required this.t,
  });

  final Paged<ClientItem> page;
  final int pageNumber;
  final int pageSize;
  final ValueChanged<ClientItem> onOpen;
  final ValueChanged<int> onPage;
  final AppLocalizations t;

  @override
  Widget build(BuildContext context) {
    final pages = (page.totalCount / pageSize).ceil().clamp(1, 1 << 20);
    return Column(
      children: [
        Expanded(
          child: Card(
            clipBehavior: Clip.antiAlias,
            child: SizedBox(
              width: double.infinity,
              child: SingleChildScrollView(
                child: DataTable(
                  showCheckboxColumn: false,
                  columns: [
                    DataColumn(label: Text(t.name)),
                    DataColumn(label: Text(t.email)),
                    DataColumn(label: Text(t.phone)),
                    DataColumn(label: Text(t.clientRegistered)),
                    DataColumn(label: Text(t.status)),
                  ],
                  rows: [
                    for (final c in page.items)
                      DataRow(
                        key: ValueKey('client_row_${c.id}'),
                        onSelectChanged: (_) => onOpen(c),
                        cells: [
                          DataCell(
                            Text(
                              c.fullName,
                              key: Key('client_cell_${c.id}'),
                              style: const TextStyle(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                          DataCell(Text(c.email)),
                          DataCell(Text(c.phone ?? '-')),
                          DataCell(
                            Text(
                              c.registeredAtUtc == null
                                  ? '-'
                                  : formatWhen(c.registeredAtUtc!),
                            ),
                          ),
                          DataCell(
                            c.isBlocked
                                ? Chip(
                                    key: Key('blocked_${c.id}'),
                                    label: Text(t.clientBlocked),
                                    backgroundColor: WorkspaceColors.danger
                                        .withValues(alpha: 0.12),
                                    side: BorderSide.none,
                                    labelStyle: const TextStyle(
                                      color: WorkspaceColors.danger,
                                      fontWeight: FontWeight.w700,
                                      fontSize: 12,
                                    ),
                                  )
                                : const SizedBox.shrink(),
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
            Text('${page.totalCount}  ·  $pageNumber / $pages'),
            IconButton(
              key: const Key('clients_prev'),
              tooltip: t.previousPage,
              onPressed: pageNumber > 1 ? () => onPage(pageNumber - 1) : null,
              icon: const Icon(Icons.chevron_left),
            ),
            IconButton(
              key: const Key('clients_next'),
              tooltip: t.nextPage,
              onPressed: pageNumber < pages
                  ? () => onPage(pageNumber + 1)
                  : null,
              icon: const Icon(Icons.chevron_right),
            ),
          ],
        ),
      ],
    );
  }
}
