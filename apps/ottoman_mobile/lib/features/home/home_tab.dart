import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:url_launcher/url_launcher.dart';

import '../gallery/gallery_page.dart';

/// Opens a link outside the app (maps, phone). A seam so the screen can be tested without a platform.
typedef UrlOpener = Future<bool> Function(Uri uri);

Future<bool> openExternally(Uri uri) =>
    launchUrl(uri, mode: LaunchMode.externalApplication);

/// Draws the small map of the salon. A seam so tests do not fetch map tiles from the internet.
typedef MapBuilder = Widget Function(
  BuildContext context,
  double latitude,
  double longitude,
);

/// A still OpenStreetMap preview with a pin. Tapping it is handled by the caller (it opens Google Maps).
Widget osmPreview(BuildContext context, double latitude, double longitude) {
  final point = LatLng(latitude, longitude);
  return FlutterMap(
    options: MapOptions(
      initialCenter: point,
      initialZoom: 16,
      interactionOptions: const InteractionOptions(flags: InteractiveFlag.none),
    ),
    children: [
      TileLayer(
        urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
        userAgentPackageName: 'ba.ottoman.ottoman_mobile',
      ),
      MarkerLayer(
        markers: [
          Marker(
            point: point,
            width: 44,
            height: 44,
            alignment: Alignment.topCenter,
            child: const Icon(
              Icons.location_on,
              size: 44,
              color: Color(0xFFD32F2F),
            ),
          ),
        ],
      ),
    ],
  );
}

/// The salon's own page: logo and a few words, "Our work", opening hours and where to find it.
class HomeTab extends StatefulWidget {
  const HomeTab({
    super.key,
    required this.salon,
    required this.gallery,
    required this.onBook,
    this.openUrl = openExternally,
    this.mapBuilder = osmPreview,
    this.today,
  });

  final SalonApi salon;
  final GalleryApi gallery;

  /// Opens the booking flow (choose a barber ...).
  final VoidCallback onBook;
  final UrlOpener openUrl;
  final MapBuilder mapBuilder;

  /// The weekday highlighted in the opening hours (0 = Sunday); the current day when null. A seam for tests.
  final int? today;

  @override
  State<HomeTab> createState() => _HomeTabState();
}

class _HomeTabState extends State<HomeTab> {
  late Future<({SalonProfile salon, List<GalleryItem> featured})> _load =
      _fetch();

  Future<({SalonProfile salon, List<GalleryItem> featured})> _fetch() async {
    final salon = await widget.salon.profile();
    // "Our work" is decoration: without it the page is still about the salon.
    final featured = await widget.gallery
        .items(featured: true, pageSize: 10)
        .then((p) => p.items)
        .catchError((_) => <GalleryItem>[]);
    return (salon: salon, featured: featured);
  }

  Future<void> _reload() async {
    final next = _fetch();
    setState(() => _load = next);
    await next.catchError((_) => (salon: _empty, featured: <GalleryItem>[]));
  }

  static const _empty = SalonProfile(name: '', hours: []);

  void _openGallery([int? categoryId]) => Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) =>
          GalleryPage(gallery: widget.gallery, initialCategoryId: categoryId),
    ),
  );

  /// The salon's own Google Maps link, or a search for its coordinates.
  static Uri _mapsUri(SalonProfile s) => s.mapsUrl != null
      ? Uri.parse(s.mapsUrl!)
      : Uri.https('www.google.com', '/maps/search/', {
          'api': '1',
          'query': '${s.latitude},${s.longitude}',
        });

  Future<void> _launch(Uri uri) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    var ok = false;
    try {
      ok = await widget.openUrl(uri);
    } on Object {
      ok = false;
    }
    if (!ok) messenger.showSnackBar(SnackBar(content: Text(t.genericError)));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return FutureBuilder(
      future: _load,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const _HomeSkeleton();
        }
        if (snap.hasError) {
          final e = snap.error;
          return ErrorView(
            message: e is AppException ? e.message : t.genericError,
            retryLabel: t.retry,
            onRetry: _reload,
          );
        }
        final data = snap.data!;
        return RefreshIndicator(
          onRefresh: _reload,
          child: ListView(
            key: const Key('home_list'),
            padding: const EdgeInsets.only(bottom: 24),
            children: [
              _Hero(salon: data.salon, onBook: widget.onBook),
              if (data.salon.description != null) ...[
                SectionHeading(t.getToKnowUs, eyebrow: t.eyebrowAbout),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: Text(
                    data.salon.description!,
                    key: const Key('salon_description'),
                    style: Theme.of(context).textTheme.bodyLarge
                        ?.copyWith(height: 1.45),
                  ),
                ),
              ],
              if (data.featured.isNotEmpty) ...[
                SectionHeading(t.ourWork, eyebrow: t.eyebrowGallery),
                _Featured(
                  items: data.featured,
                  onOpen: (item) => _openGallery(item.categoryId),
                ),
              ],
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
                child: OutlinedButton.icon(
                  key: const Key('open_gallery'),
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size.fromHeight(48),
                  ),
                  onPressed: _openGallery,
                  icon: const Icon(Icons.photo_library_outlined),
                  label: Text(t.viewGallery),
                ),
              ),
              if (data.salon.phone != null ||
                  data.salon.instagramUrl != null ||
                  data.salon.facebookUrl != null) ...[
                SectionHeading(t.connectWithUs, eyebrow: t.eyebrowContact),
                _Connect(
                  salon: data.salon,
                  onCall: () => _launch(
                    Uri(
                      scheme: 'tel',
                      path: data.salon.phone!.replaceAll(RegExp(r'[^\d+]'), ''),
                    ),
                  ),
                  onInstagram: () =>
                      _launch(Uri.parse(data.salon.instagramUrl!)),
                  onFacebook: () => _launch(Uri.parse(data.salon.facebookUrl!)),
                ),
              ],
              if (data.salon.address != null ||
                  data.salon.mapsUrl != null ||
                  data.salon.hasLocation) ...[
                SectionHeading(t.findUsHere, eyebrow: t.eyebrowLocation),
                _Contact(
                  salon: data.salon,
                  mapBuilder: widget.mapBuilder,
                  onMaps: () => _launch(_mapsUri(data.salon)),
                ),
              ],
              if (data.salon.hours.isNotEmpty) ...[
                SectionHeading(t.workingHours, eyebrow: t.eyebrowSchedule),
                _Hours(
                  hours: data.salon.hours,
                  today: widget.today ?? DateTime.now().weekday % 7,
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}

class _Hero extends StatelessWidget {
  const _Hero({required this.salon, required this.onBook});

  final SalonProfile salon;
  final VoidCallback onBook;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(20, 28, 20, 8),
      decoration: BoxDecoration(
        borderRadius: const BorderRadius.vertical(bottom: Radius.circular(36)),
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [colors.primaryContainer, colors.surface],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            key: const Key('salon_logo'),
            width: 96,
            height: 96,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colors.surfaceContainer,
              border: Border.all(color: colors.primary, width: 2),
            ),
            clipBehavior: Clip.antiAlias,
            child: salon.logoUrl == null
                ? const Padding(
                    padding: EdgeInsets.all(16),
                    child: OttomanLogo(
                      variant: LogoVariant.emblem,
                      light: true,
                    ),
                  )
                : NetworkPicture(url: salon.logoUrl),
          ),
          const SizedBox(height: 18),
          Text(
            t.welcomeTo(salon.name),
            key: const Key('salon_name'),
            style: Theme.of(context).textTheme.headlineMedium
                ?.copyWith(fontWeight: FontWeight.w800, height: 1.15),
          ),
          const SizedBox(height: 20),
          FilledButton.icon(
            key: const Key('book_now'),
            onPressed: onBook,
            icon: const Icon(Icons.calendar_month_outlined),
            label: Text(t.bookAppointment),
          ),
          const SizedBox(height: 16),
        ],
      ),
    );
  }
}

class _Connect extends StatelessWidget {
  const _Connect({
    required this.salon,
    required this.onCall,
    required this.onInstagram,
    required this.onFacebook,
  });

  final SalonProfile salon;
  final VoidCallback onCall;
  final VoidCallback onInstagram;
  final VoidCallback onFacebook;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final buttons = <Widget>[
      if (salon.phone != null)
        _SocialButton(
          key: const Key('call_salon'),
          icon: Icons.call_rounded,
          label: t.callSalon,
          onTap: onCall,
        ),
      if (salon.instagramUrl != null)
        _SocialButton(
          key: const Key('open_instagram'),
          icon: Icons.camera_alt_rounded,
          label: t.instagram,
          onTap: onInstagram,
        ),
      if (salon.facebookUrl != null)
        _SocialButton(
          key: const Key('open_facebook'),
          icon: Icons.facebook_rounded,
          label: t.facebook,
          onTap: onFacebook,
        ),
    ];
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Row(
        children: [
          for (var i = 0; i < buttons.length; i++) ...[
            if (i > 0) const SizedBox(width: 12),
            Expanded(child: buttons[i]),
          ],
        ],
      ),
    );
  }
}

class _SocialButton extends StatelessWidget {
  const _SocialButton({
    super.key,
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: scheme.surfaceContainer,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 18),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: scheme.outlineVariant),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 30, color: scheme.primary),
              const SizedBox(height: 8),
              Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 13,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Featured extends StatelessWidget {
  const _Featured({required this.items, required this.onOpen});

  final List<GalleryItem> items;
  final ValueChanged<GalleryItem> onOpen;

  @override
  Widget build(BuildContext context) => SizedBox(
    height: 200,
    child: ListView.separated(
      key: const Key('featured_list'),
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: 20),
      itemCount: items.length,
      separatorBuilder: (_, _) => const SizedBox(width: 12),
      itemBuilder: (context, i) => GestureDetector(
        key: Key('featured_${items[i].id}'),
        onTap: () => onOpen(items[i]),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(16),
          child: SizedBox(
            width: 150,
            child: Stack(
              fit: StackFit.expand,
              children: [
                NetworkPicture(url: items[i].imageUrl),
                Positioned(
                  left: 0,
                  right: 0,
                  bottom: 0,
                  child: Container(
                    padding: const EdgeInsets.fromLTRB(10, 20, 10, 8),
                    decoration: const BoxDecoration(
                      gradient: LinearGradient(
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                        colors: [Colors.transparent, Color(0xB3000000)],
                      ),
                    ),
                    child: Text(
                      items[i].categoryName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}

class _Hours extends StatelessWidget {
  const _Hours({required this.hours, required this.today});

  final List<OpeningHours> hours;
  final int today;

  static String _hhmm(int m) =>
      '${(m ~/ 60).toString().padLeft(2, '0')}:${(m % 60).toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    String name(int day) => switch (day) {
      1 => t.dayMonday,
      2 => t.dayTuesday,
      3 => t.dayWednesday,
      4 => t.dayThursday,
      5 => t.dayFriday,
      6 => t.daySaturday,
      _ => t.daySunday,
    };
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Card(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Column(
            children: [
              for (final h in hours)
                Container(
                  key: Key('hours_row_${h.day}'),
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          name(h.day),
                          style: TextStyle(
                            fontWeight: h.day == today
                                ? FontWeight.w800
                                : FontWeight.w500,
                            color: h.day == today ? colors.primary : null,
                          ),
                        ),
                      ),
                      Text(
                        h.isClosed
                            ? t.closedDay
                            : '${_hhmm(h.openMinutes!)} – ${_hhmm(h.closeMinutes!)}',
                        style: TextStyle(
                          fontWeight: h.day == today
                              ? FontWeight.w800
                              : FontWeight.w500,
                          color: h.isClosed
                              ? colors.onSurfaceVariant
                              : (h.day == today ? colors.primary : null),
                        ),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Contact extends StatelessWidget {
  const _Contact({
    required this.salon,
    required this.mapBuilder,
    required this.onMaps,
  });

  final SalonProfile salon;
  final MapBuilder mapBuilder;
  final VoidCallback onMaps;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Card(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (salon.hasLocation) ...[
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: SizedBox(
                    height: 190,
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        mapBuilder(context, salon.latitude!, salon.longitude!),
                        Positioned.fill(
                          child: Material(
                            color: Colors.transparent,
                            child: InkWell(
                              key: const Key('salon_map'),
                              onTap: onMaps,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 14),
              ],
              if (salon.address != null)
                Row(
                  children: [
                    const Icon(Icons.place_outlined),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        salon.address!,
                        key: const Key('salon_address'),
                      ),
                    ),
                  ],
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _HomeSkeleton extends StatelessWidget {
  const _HomeSkeleton();

  @override
  Widget build(BuildContext context) => const Padding(
    padding: EdgeInsets.all(20),
    child: Column(
      children: [
        SizedBox(height: 12),
        SkeletonBox(width: 112, height: 112, radius: 56),
        SizedBox(height: 16),
        SkeletonBox(width: 180, height: 22),
        SizedBox(height: 10),
        SkeletonBox(height: 14),
        SizedBox(height: 6),
        SkeletonBox(height: 14),
        SizedBox(height: 28),
        SkeletonBox(height: 200, radius: 16),
        SizedBox(height: 20),
        SkeletonBox(height: 150, radius: 16),
      ],
    ),
  );
}
