import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// A picture picked on the phone, ready to upload.
class PickedPhoto {
  const PickedPhoto(this.bytes, this.name);

  final List<int> bytes;
  final String name;
}

/// The phone's gallery behind a port, so the profile screen is testable without the plugin.
abstract class PhotoChooser {
  const PhotoChooser();

  /// Null when the person backed out.
  Future<PickedPhoto?> choose();
}

/// Picks from the gallery, scaled down on the phone (the server still checks type and size).
class GalleryPhotoChooser extends PhotoChooser {
  const GalleryPhotoChooser();

  @override
  Future<PickedPhoto?> choose() async {
    final file = await ImagePicker().pickImage(
      source: ImageSource.gallery,
      maxWidth: 1600,
      maxHeight: 1600,
      imageQuality: 85,
    );
    if (file == null) return null;
    return PickedPhoto(await file.readAsBytes(), file.name);
  }
}

/// The account's own picture with buttons to set, replace, adjust or remove it. Every change goes to the server at
/// once, which checks the file (only real JPEG, PNG or WebP up to 5 MB). A new picture is uploaded first and then
/// opened in the zoom editor, so the preview is the server's real crop around the face; the zoom button opens the
/// same editor for the current picture.
class ProfilePhoto extends StatefulWidget {
  const ProfilePhoto({
    super.key,
    required this.auth,
    this.chooser = const GalleryPhotoChooser(),
  });

  final AuthController auth;
  final PhotoChooser chooser;

  @override
  State<ProfilePhoto> createState() => _ProfilePhotoState();
}

class _ProfilePhotoState extends State<ProfilePhoto> {
  bool _busy = false;

  Future<void> _run(Future<void> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _busy = true);
    try {
      await action();
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.detail)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _change() async {
    final picked = await widget.chooser.choose();
    if (picked == null || !mounted) return;
    var uploaded = false;
    await _run(() async {
      await widget.auth.setOwnPhoto(picked.bytes, picked.name);
      uploaded = true;
    });
    if (uploaded && mounted) await _adjust();
  }

  /// Opens the zoom editor on the current picture and saves the zoom only when it changed.
  Future<void> _adjust() async {
    final user = widget.auth.user;
    final url = user?.photoUrl;
    if (user == null || url == null || url.isEmpty) return;
    final current = user.photoZoom ?? AvatarZoom.min;
    final chosen = await showAvatarZoomSheet(
      context,
      photoUrl: url,
      zoom: current,
      gender: user.gender,
      seed: user.id,
    );
    if (chosen == null || chosen == current || !mounted) return;
    await _run(() => widget.auth.setOwnPhotoZoom(chosen));
  }

  Future<void> _remove() async {
    final t = AppLocalizations.of(context);
    final ok = await confirmAction(
      context,
      title: t.removePicture,
      message: t.removePictureQuestion,
      confirmLabel: t.removePictureShort,
    );
    if (ok && mounted) await _run(widget.auth.clearOwnPhoto);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return ListenableBuilder(
      listenable: widget.auth,
      builder: (context, _) {
        final user = widget.auth.user;
        final hasPhoto = (user?.photoUrl ?? '').isNotEmpty;
        return Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Stack(
              alignment: Alignment.center,
              children: [
                PersonAvatar(
                  key: const Key('profile_photo'),
                  photoUrl: user?.photoUrl,
                  gender: user?.gender,
                  seed: user?.id,
                  radius: 38,
                ),
                if (_busy) const CircularProgressIndicator(),
              ],
            ),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                IconButton(
                  key: const Key('photo_change'),
                  tooltip: hasPhoto ? t.changePhoto : t.addPicture,
                  icon: const Icon(Icons.photo_camera_outlined, size: 20),
                  onPressed: _busy ? null : _change,
                ),
                if (hasPhoto)
                  IconButton(
                    key: const Key('photo_zoom'),
                    tooltip: t.adjustPicture,
                    icon: const Icon(Icons.zoom_in, size: 20),
                    onPressed: _busy ? null : _adjust,
                  ),
                if (hasPhoto)
                  IconButton(
                    key: const Key('photo_remove'),
                    tooltip: t.removePicture,
                    icon: const Icon(Icons.delete_outline, size: 20),
                    onPressed: _busy ? null : _remove,
                  ),
              ],
            ),
          ],
        );
      },
    );
  }
}
