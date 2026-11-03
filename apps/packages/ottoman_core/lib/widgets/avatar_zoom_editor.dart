import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import 'person_avatar.dart';

/// Zoom of an avatar around the face, 1 (Cloudinary's default crop) to 2 (twice as close).
abstract final class AvatarZoom {
  static const min = 1.0;
  static const max = 2.0;

  /// The avatar URL from the server carries Cloudinary's `z_` crop value (the inverse of the zoom). Swapping it gives a
  /// preview of another zoom without asking the server; a URL without one is returned as it is.
  static String withZoom(String url, double zoom) {
    final z = (1 / zoom.clamp(min, max))
        .toStringAsFixed(2)
        .replaceFirst(RegExp(r'\.?0+$'), '');
    return url.replaceFirst(RegExp(r'z_[0-9.]+'), 'z_$z');
  }
}

/// A round preview of the cropped photo with a zoom slider and a reset. [onChangeEnd] gets the chosen zoom once the
/// slider is released (or reset is pressed); the owner decides when to save it.
class AvatarZoomEditor extends StatefulWidget {
  const AvatarZoomEditor({
    super.key,
    required this.photoUrl,
    required this.zoom,
    required this.onChangeEnd,
    this.gender,
    this.seed,
  });

  final String photoUrl;
  final double zoom;
  final ValueChanged<double> onChangeEnd;
  final String? gender;
  final String? seed;

  @override
  State<AvatarZoomEditor> createState() => _AvatarZoomEditorState();
}

class _AvatarZoomEditorState extends State<AvatarZoomEditor> {
  double? _live;

  @override
  void didUpdateWidget(covariant AvatarZoomEditor old) {
    super.didUpdateWidget(old);
    if (old.zoom != widget.zoom) _live = null;
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final value = (_live ?? widget.zoom).clamp(AvatarZoom.min, AvatarZoom.max);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        PersonAvatar(
          key: const Key('avatar_zoom_preview'),
          radius: 44,
          gender: widget.gender,
          seed: widget.seed,
          photoUrl: AvatarZoom.withZoom(widget.photoUrl, widget.zoom),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Text(
                    t.zoom,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(width: 8),
                  Text(
                    '${value.toStringAsFixed(1)}\u00d7',
                    key: const Key('avatar_zoom_value'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
                  ),
                  const Spacer(),
                  TextButton(
                    key: const Key('avatar_zoom_reset'),
                    onPressed:
                        value == AvatarZoom.min && widget.zoom == AvatarZoom.min
                        ? null
                        : () {
                            setState(() => _live = null);
                            widget.onChangeEnd(AvatarZoom.min);
                          },
                    child: Text(t.resetZoom),
                  ),
                ],
              ),
              Slider(
                key: const Key('avatar_zoom_slider'),
                min: AvatarZoom.min,
                max: AvatarZoom.max,
                divisions: 20,
                value: value,
                onChanged: (v) => setState(() => _live = v),
                onChangeEnd: (v) {
                  setState(() => _live = null);
                  widget.onChangeEnd(double.parse(v.toStringAsFixed(2)));
                },
              ),
              Text(
                t.zoomHint,
                style: TextStyle(
                  fontSize: 12,
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
