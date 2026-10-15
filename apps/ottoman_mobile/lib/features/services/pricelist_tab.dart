import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Services & prices. For employees, [employeeId] limits it to the services assigned to them.
class PricelistTab extends StatelessWidget {
  const PricelistTab({super.key, required this.catalog, this.employeeId});

  final CatalogApi catalog;
  final String? employeeId;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AsyncView<List<Service>>(
      load: () => catalog.services(employeeId: employeeId),
      skeleton: const ListSkeleton(),
      isEmpty: (s) => s.isEmpty,
      builder: (context, services, reload) => RefreshIndicator(
        onRefresh: reload,
        child: ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: services.length,
          separatorBuilder: (_, _) => const SizedBox(height: 12),
          itemBuilder: (context, i) {
            final s = services[i];
            return Card(
              child: ListTile(
                title: Text(
                  s.name,
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                subtitle: s.description == null ? null : Text(s.description!),
                trailing: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      '${s.price.toStringAsFixed(2)} ${t.currencyShort}',
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    Text(
                      t.minutesShort(s.durationMinutes),
                      style: const TextStyle(fontSize: 12),
                    ),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}
