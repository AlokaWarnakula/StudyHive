import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// The handful of pieces every reference screen is assembled from. They map 1:1
/// onto the reference document's own .tile / .kv / .lbl / .big / .fnote / .tag /
/// .seg / .stepper / .tl / .ph / .mnav classes, so a screen here reads like the
/// frame it was drawn from. Mirrors web/src/components/ui.tsx.

/* ── type ─────────────────────────────────────────────────────────────────── */

/// .lbl — Barlow Condensed 11px, tracked out, uppercase, muted.
class Lbl extends StatelessWidget {
  final String text;
  const Lbl(this.text, {super.key});

  @override
  Widget build(BuildContext context) => Text(
        text.toUpperCase(),
        style: headingStyle(
            fontSize: 11, color: AppColors.muted, letterSpacing: 1.1),
      );
}

/// .big — Barlow Condensed 26px, tight leading. The screen's headline number
/// or room name.
class Big extends StatelessWidget {
  final String text;
  final Color? color;
  const Big(this.text, {super.key, this.color});

  @override
  Widget build(BuildContext context) => Text(
        text,
        style: headingStyle(fontSize: 26, height: 1.05, color: color),
      );
}

/// .fnote — the 12px supporting line under a title.
class FNote extends StatelessWidget {
  final String text;
  final TextAlign? textAlign;
  final int? maxLines;
  const FNote(this.text, {super.key, this.textAlign, this.maxLines});

  @override
  Widget build(BuildContext context) => Text(
        text,
        textAlign: textAlign,
        maxLines: maxLines,
        overflow: maxLines == null ? null : TextOverflow.ellipsis,
        style: const TextStyle(fontSize: 12, height: 1.45, color: AppColors.note),
      );
}

/// h3/h4-weight screen heading inside a body (not the app bar).
class Heading extends StatelessWidget {
  final String text;
  final double fontSize;
  final TextAlign? textAlign;
  const Heading(this.text, {super.key, this.fontSize = 25, this.textAlign});

  @override
  Widget build(BuildContext context) => Text(
        text,
        textAlign: textAlign,
        style: headingStyle(
            fontSize: fontSize, height: 1.12, letterSpacing: -0.3),
      );
}

/* ── tags ─────────────────────────────────────────────────────────────────── */

/// The reference's three tag tones. There is no red/green/amber in the palette.
enum TagTone {
  /// .tag-accent — the affirmative state (Approved, Free now, Working, Active).
  accent,

  /// .tag-outline — the in-flight state (Waiting, Pending, Your pick).
  outline,

  /// .tag-neutral — the inert state (Completed, Rejected, Booked, Out of stock).
  neutral,
}

class ShTag extends StatelessWidget {
  final String label;
  final TagTone tone;

  const ShTag(this.label, {super.key, this.tone = TagTone.neutral});

  /// Maps a workflow/domain status onto the tone the reference gives it.
  factory ShTag.forStatus(String status, {Key? key}) =>
      ShTag(status, key: key, tone: toneFor(status));

  static TagTone toneFor(String status) {
    final s = status.toLowerCase();
    if (s.startsWith('free') ||
        s == 'approved' ||
        s == 'active' ||
        s == 'working' ||
        s == 'checkedin') {
      return TagTone.accent;
    }
    if (s == 'waiting' ||
        s == 'pending' ||
        s == 'pendingapproval' ||
        s == 'submitted' ||
        s == 'processing' ||
        s == 'started' ||
        s == 'inprogress' ||
        s == 'proposed' ||
        s == 'draft' ||
        s == 'revisionrequested' ||
        s == 'your pick' ||
        s.startsWith('maintenance today') ||
        s.startsWith('waiting for')) {
      return TagTone.outline;
    }
    return TagTone.neutral;
  }

  @override
  Widget build(BuildContext context) {
    final (Color? background, Color foreground, Border? border) =
        switch (tone) {
      TagTone.accent => (AppColors.accent100, AppColors.accent800, null),
      TagTone.neutral => (AppColors.neutral100, AppColors.neutral800, null),
      TagTone.outline => (
          null,
          AppColors.accent,
          Border.all(color: AppColors.accent)
        ),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(
          color: background, border: border, borderRadius: AppRadius.tag),
      child: Text(
        label,
        style: TextStyle(fontSize: 11, letterSpacing: 0.2, color: foreground),
      ),
    );
  }
}

/* ── tile ─────────────────────────────────────────────────────────────────── */

/// .tile — a 1px hairline box, square corners, 14px padding, 8px gap. Set
/// [accented] for the screen's active panel and [tinted] for the accent-100 fill
/// the reference uses on a summary tile.
class Tile extends StatelessWidget {
  final List<Widget> children;
  final bool accented;
  final bool tinted;
  final double? opacity;
  final EdgeInsetsGeometry padding;
  final VoidCallback? onTap;
  final CrossAxisAlignment crossAxisAlignment;
  final double gap;

  const Tile({
    super.key,
    required this.children,
    this.accented = false,
    this.tinted = false,
    this.opacity,
    this.padding = const EdgeInsets.all(14),
    this.onTap,
    this.crossAxisAlignment = CrossAxisAlignment.start,
    this.gap = 8,
  });

  /// The horizontal variant (a room row, a time slot) — .tile with
  /// flex-direction:row.
  const Tile.row({
    super.key,
    required this.children,
    this.accented = false,
    this.tinted = false,
    this.opacity,
    this.padding = const EdgeInsets.all(14),
    this.onTap,
    this.gap = 12,
  }) : crossAxisAlignment = CrossAxisAlignment.center;

  bool get _isRow => crossAxisAlignment == CrossAxisAlignment.center;

  @override
  Widget build(BuildContext context) {
    final spaced = <Widget>[];
    for (var i = 0; i < children.length; i++) {
      if (i > 0) {
        spaced.add(_isRow ? SizedBox(width: gap) : SizedBox(height: gap));
      }
      spaced.add(children[i]);
    }

    Widget content = Container(
      width: double.infinity,
      padding: padding,
      decoration: BoxDecoration(
        color: tinted ? AppColors.accent100 : null,
        border: Border.all(
            color: accented || tinted ? AppColors.accent : AppColors.divider),
      ),
      child: _isRow
          ? Row(crossAxisAlignment: CrossAxisAlignment.center, children: spaced)
          : Column(crossAxisAlignment: CrossAxisAlignment.start, children: spaced),
    );

    if (onTap != null) {
      content = InkWell(onTap: onTap, child: content);
    }
    if (opacity != null) {
      content = Opacity(opacity: opacity!, child: content);
    }
    return content;
  }
}

/* ── key/value row ────────────────────────────────────────────────────────── */

/// .kv — label on the left, a 500-weight value on the right.
class Kv extends StatelessWidget {
  final String label;
  final String? value;
  final Widget? trailing;
  final Widget? leading;

  const Kv(this.label, this.value, {super.key})
      : trailing = null,
        leading = null;

  /// A .kv whose right-hand side is a widget (a tag, a link, a stepper).
  const Kv.widget({super.key, required this.label, required this.trailing})
      : value = null,
        leading = null;

  /// A .kv whose left-hand side is a widget too.
  const Kv.both({super.key, required this.leading, required this.trailing})
      : label = '',
        value = null;

  @override
  Widget build(BuildContext context) {
    // Text sides flex so a long label wraps; widget sides (a tag, a stepper)
    // keep their intrinsic width and stay pinned to their edge.
    final left = leading != null
        ? Flexible(child: leading!)
        : Flexible(
            child:
                Text(label, style: const TextStyle(fontSize: 14, height: 1.4)),
          );
    final right = trailing != null
        ? trailing!
        : Flexible(
            child: Text(
              value ?? '',
              textAlign: TextAlign.right,
              style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500),
            ),
          );

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [left, const SizedBox(width: 12), right],
    );
  }
}

/// A .kv value rendered at .big size — the total row of a cost breakdown.
class KvTotal extends StatelessWidget {
  final String label;
  final String value;
  const KvTotal(this.label, this.value, {super.key});

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.only(top: 10),
        decoration: const BoxDecoration(
            border: Border(top: BorderSide(color: AppColors.divider))),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Text(label,
                style: const TextStyle(
                    fontSize: 14, fontWeight: FontWeight.w500)),
            Big(value),
          ],
        ),
      );
}

/* ── buttons ──────────────────────────────────────────────────────────────── */

/// .btn.btn-primary.btn-lg — full width, 14px padding, 17px condensed label.
class PrimaryButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final double fontSize;
  final EdgeInsetsGeometry? padding;

  const PrimaryButton(this.label,
      {super.key,
      required this.onPressed,
      this.icon,
      this.fontSize = 17,
      this.padding});

  @override
  Widget build(BuildContext context) {
    final style = FilledButton.styleFrom(
      padding: padding ?? const EdgeInsets.symmetric(vertical: 14),
      textStyle: headingStyle(fontSize: fontSize),
    );
    return SizedBox(
      width: double.infinity,
      child: icon == null
          ? FilledButton(onPressed: onPressed, style: style, child: Text(label))
          : FilledButton.icon(
              onPressed: onPressed,
              style: style,
              icon: Icon(icon, size: fontSize + 3),
              label: Text(label)),
    );
  }
}

/// .btn.btn-secondary.btn-lg — hairline border, text-coloured ink.
class SecondaryButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final Color? foreground;
  final Color? borderColor;

  const SecondaryButton(this.label,
      {super.key,
      required this.onPressed,
      this.icon,
      this.foreground,
      this.borderColor});

  @override
  Widget build(BuildContext context) {
    final style = OutlinedButton.styleFrom(
      padding: const EdgeInsets.symmetric(vertical: 14),
      foregroundColor: foreground,
      side: borderColor == null ? null : BorderSide(color: borderColor!),
    );
    return SizedBox(
      width: double.infinity,
      child: icon == null
          ? OutlinedButton(
              onPressed: onPressed, style: style, child: Text(label))
          : OutlinedButton.icon(
              onPressed: onPressed,
              style: style,
              icon: Icon(icon, size: 20),
              label: Text(label)),
    );
  }
}

/// .btn.btn-ghost.btn-lg — accent ink, no border.
class GhostButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  const GhostButton(this.label, {super.key, required this.onPressed});

  @override
  Widget build(BuildContext context) => SizedBox(
        width: double.infinity,
        child: TextButton(onPressed: onPressed, child: Text(label)),
      );
}

/// An inline `<a>` — the "Edit" / "See reason" links inside a tile.
class ShLink extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final double fontSize;
  const ShLink(this.label,
      {super.key, required this.onPressed, this.fontSize = 14});

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onPressed,
        child: Text(
          label,
          style: TextStyle(
            fontSize: fontSize,
            color: AppColors.accent,
            decoration: TextDecoration.underline,
            decorationColor: AppColors.accent,
          ),
        ),
      );
}

/// The square −/+ pair either side of a value (M-04 group size, M-05 quantity).
class CounterControl extends StatelessWidget {
  final int value;
  final ValueChanged<int>? onChanged;
  final int min;
  final int max;
  final double size;
  final double valueFontSize;

  const CounterControl({
    super.key,
    required this.value,
    required this.onChanged,
    this.min = 0,
    this.max = 999,
    this.size = 48,
    this.valueFontSize = 26,
  });

  @override
  Widget build(BuildContext context) {
    Widget button(String glyph, VoidCallback? onPressed) => SizedBox(
          width: size,
          height: size,
          child: OutlinedButton(
            onPressed: onPressed,
            style: OutlinedButton.styleFrom(
              padding: EdgeInsets.zero,
              minimumSize: Size(size, size),
              textStyle: headingStyle(fontSize: 20),
            ),
            child: Text(glyph),
          ),
        );

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        button('−',
            value > min && onChanged != null ? () => onChanged!(value - 1) : null),
        SizedBox(
          width: valueFontSize > 20 ? 46 : 38,
          child: Text('$value',
              textAlign: TextAlign.center,
              style: headingStyle(fontSize: valueFontSize, height: 1.05)),
        ),
        button('+',
            value < max && onChanged != null ? () => onChanged!(value + 1) : null),
      ],
    );
  }
}

/* ── forms ────────────────────────────────────────────────────────────────── */

/// .field — a 12px label above its control. Any control can sit in [child];
/// [ShTextField] is the common case.
class Field extends StatelessWidget {
  final String label;
  final Widget child;

  const Field({super.key, required this.label, required this.child});

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.only(bottom: 5),
            child: Text(label,
                style: const TextStyle(
                    fontSize: 12, color: AppColors.fieldLabel)),
          ),
          child,
        ],
      );
}

/// .field + .input. The label sits above the box exactly as the reference draws
/// it, so the control itself carries a [ValueKey] of `field:<label>` for tests
/// to target.
class ShTextField extends StatelessWidget {
  final String label;
  final TextEditingController? controller;
  final String? initialValue;
  final bool obscureText;
  final int maxLines;
  final TextInputType? keyboardType;
  final String? Function(String?)? validator;
  final void Function(String)? onFieldSubmitted;
  final List<String>? autofillHints;
  final bool readOnly;
  final VoidCallback? onTap;
  final String? hintText;

  const ShTextField({
    super.key,
    required this.label,
    this.controller,
    this.initialValue,
    this.obscureText = false,
    this.maxLines = 1,
    this.keyboardType,
    this.validator,
    this.onFieldSubmitted,
    this.autofillHints,
    this.readOnly = false,
    this.onTap,
    this.hintText,
  });

  @override
  Widget build(BuildContext context) => Field(
        label: label,
        child: TextFormField(
          key: ValueKey('field:$label'),
          controller: controller,
          initialValue: initialValue,
          obscureText: obscureText,
          maxLines: obscureText ? 1 : maxLines,
          keyboardType: keyboardType,
          validator: validator,
          onFieldSubmitted: onFieldSubmitted,
          autofillHints: autofillHints,
          readOnly: readOnly,
          onTap: onTap,
          style: const TextStyle(fontSize: 14),
          decoration: InputDecoration(
            hintText: hintText,
            // .input { min-height: 48px } on every mobile frame.
            constraints: BoxConstraints(minHeight: maxLines > 1 ? 84 : 48),
          ),
        ),
      );
}

/// .seg — a hairline segmented control; the checked option takes the accent
/// fill. Used for M-02 year of study and M-12 booking tabs.
class Segmented<T> extends StatelessWidget {
  final List<(T, String)> options;
  final T value;
  final ValueChanged<T> onChanged;

  const Segmented({
    super.key,
    required this.options,
    required this.value,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        border: Border.all(color: AppColors.divider),
        borderRadius: AppRadius.md,
      ),
      clipBehavior: Clip.antiAlias,
      child: Row(
        children: [
          for (var i = 0; i < options.length; i++)
            Expanded(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  border: i == 0
                      ? null
                      : const Border(
                          left: BorderSide(color: AppColors.divider)),
                ),
                child: InkWell(
                  onTap: () => onChanged(options[i].$1),
                  child: Container(
                    height: 44,
                    alignment: Alignment.center,
                    color: options[i].$1 == value ? AppColors.accent : null,
                    child: Text(
                      options[i].$2,
                      style: TextStyle(
                        fontSize: 13,
                        color: options[i].$1 == value
                            ? AppColors.bg
                            : AppColors.text,
                      ),
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// A row of filter tags (M-09). The selected chip is tag-accent, the rest
/// tag-outline.
class FilterTags extends StatelessWidget {
  final List<String> options;
  final String value;
  final ValueChanged<String> onChanged;

  const FilterTags({
    super.key,
    required this.options,
    required this.value,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) => Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          for (final option in options)
            GestureDetector(
              onTap: () => onChanged(option),
              child: ShTag(option,
                  tone: option == value ? TagTone.accent : TagTone.outline),
            ),
        ],
      );
}

/* ── progress ─────────────────────────────────────────────────────────────── */

/// .stepper — the three 5px segments across the top of the booking flow.
class StepperBar extends StatelessWidget {
  final int step;
  final int total;

  const StepperBar({super.key, required this.step, this.total = 3});

  @override
  Widget build(BuildContext context) => Row(
        children: [
          for (var i = 0; i < total; i++) ...[
            if (i > 0) const SizedBox(width: 6),
            Expanded(
              child: Container(
                height: 5,
                color:
                    i <= step ? AppColors.accent : AppColors.neutral300,
              ),
            ),
          ],
        ],
      );
}

/// The flat meter under "Bookings this week" (M-03).
class Meter extends StatelessWidget {
  final double percent;
  final double height;
  const Meter({super.key, required this.percent, this.height = 8});

  @override
  Widget build(BuildContext context) => SizedBox(
        height: height,
        child: Stack(
          children: [
            Container(color: AppColors.neutral300),
            FractionallySizedBox(
              widthFactor: percent.clamp(0, 1),
              child: Container(color: AppColors.accent),
            ),
          ],
        ),
      );
}

/* ── timeline ─────────────────────────────────────────────────────────────── */

/// done = filled square dot, current = outlined dot, waiting = greyed dot.
enum TlState { done, current, waiting }

class TimelineStep {
  final String title;
  final String? detail;
  final TlState state;

  const TimelineStep(this.title, {this.detail, this.state = TlState.waiting});
}

/// .tl — the vertical run of dots joined by 1px stems (M-07, M-13).
class Timeline extends StatelessWidget {
  final List<TimelineStep> steps;
  const Timeline({super.key, required this.steps});

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[];
    for (var i = 0; i < steps.length; i++) {
      final step = steps[i];
      final waiting = step.state == TlState.waiting;
      rows.add(
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 22,
              child: Padding(
                padding: const EdgeInsets.only(left: 5, top: 5),
                child: Container(
                  width: 11,
                  height: 11,
                  decoration: BoxDecoration(
                    color: step.state == TlState.done
                        ? AppColors.accent
                        : null,
                    border: Border.all(
                      color: waiting ? AppColors.neutral400 : AppColors.accent,
                      width: 1.5,
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(step.title,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w500,
                        color: waiting ? AppColors.muted : AppColors.text,
                      )),
                  if (step.detail != null) FNote(step.detail!),
                ],
              ),
            ),
          ],
        ),
      );
      if (i < steps.length - 1) {
        rows.add(
          Padding(
            padding: const EdgeInsets.only(left: 10),
            child: Container(width: 1, height: 26, color: AppColors.neutral300),
          ),
        );
      }
    }

    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: rows);
  }
}

/* ── hatched placeholder ──────────────────────────────────────────────────── */

/// .ph — the diagonally hatched box the reference uses wherever a photo, avatar
/// or illustration will go. Keeping the hatch (rather than inventing artwork)
/// is deliberate: an unfilled slot must read as unfilled.
class Ph extends StatelessWidget {
  final String? label;
  final double? width;
  final double? height;

  const Ph({super.key, this.label, this.width, this.height});

  @override
  Widget build(BuildContext context) => Container(
        width: width,
        height: height,
        decoration: BoxDecoration(border: Border.all(color: AppColors.divider)),
        // The hatch runs diagonally past the box on both sides; clip it to the
        // frame the way the CSS background does.
        clipBehavior: Clip.hardEdge,
        child: CustomPaint(
          painter: _HatchPainter(),
          child: Center(
            child: label == null
                ? null
                : Container(
                    color: AppColors.bg,
                    padding:
                        const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    child: Text(
                      label!.toUpperCase(),
                      style: const TextStyle(
                        fontFamily: 'monospace',
                        fontSize: 10,
                        letterSpacing: 0.8,
                        color: AppColors.muted,
                      ),
                    ),
                  ),
          ),
        ),
      );
}

/// repeating-linear-gradient(135deg, transparent 0 7px, text@7% 7px 8px)
class _HatchPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = AppColors.text.withValues(alpha: 0.07)
      ..strokeWidth = 1;
    const spacing = 8.0;
    for (var x = -size.height; x < size.width; x += spacing) {
      canvas.drawLine(Offset(x, 0), Offset(x + size.height, size.height), paint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/* ── chrome ───────────────────────────────────────────────────────────────── */

/// .mnav — four flat destinations under a hairline, accent-700 when current.
/// Material's NavigationBar draws a pill indicator the reference does not have.
class BottomNav extends StatelessWidget {
  final int index;
  final ValueChanged<int> onChanged;

  static const destinations = <(IconData, String)>[
    (Icons.home_outlined, 'Home'),
    (Icons.meeting_room_outlined, 'Rooms'),
    (Icons.event_available_outlined, 'Bookings'),
    (Icons.person_outline, 'Profile'),
  ];

  const BottomNav({super.key, required this.index, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.bg,
        border: Border(top: BorderSide(color: AppColors.divider)),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.only(top: 10, bottom: 8),
          child: Row(
            children: [
              for (var i = 0; i < destinations.length; i++)
                Expanded(
                  child: InkWell(
                    onTap: () => onChanged(i),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 4),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            destinations[i].$1,
                            size: 22,
                            color: i == index
                                ? AppColors.accent700
                                : AppColors.text,
                          ),
                          const SizedBox(height: 3),
                          Text(
                            destinations[i].$2,
                            style: TextStyle(
                              fontSize: 11,
                              color: i == index
                                  ? AppColors.accent700
                                  : AppColors.muted,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The .mbody column: 18px/16px padding and a 14px gap between children.
class ScreenBody extends StatelessWidget {
  final List<Widget> children;
  final EdgeInsetsGeometry padding;
  final double gap;
  final ScrollController? controller;

  const ScreenBody({
    super.key,
    required this.children,
    this.padding = const EdgeInsets.fromLTRB(16, 18, 16, 24),
    this.gap = 14,
    this.controller,
  });

  @override
  Widget build(BuildContext context) {
    final spaced = <Widget>[];
    for (var i = 0; i < children.length; i++) {
      if (i > 0) spaced.add(SizedBox(height: gap));
      spaced.add(children[i]);
    }
    return ListView(
      controller: controller,
      padding: padding,
      children: spaced,
    );
  }
}

/* ── notices ──────────────────────────────────────────────────────────────── */

/// The inline error box from M-01 — an accent-bordered panel, never a popup.
class InlineError extends StatelessWidget {
  final String message;
  const InlineError(this.message, {super.key});

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        decoration: BoxDecoration(
          color: AppColors.accent100,
          border: Border.all(color: AppColors.accent),
        ),
        child: Text(
          message,
          style: const TextStyle(fontSize: 13, color: AppColors.accent800),
        ),
      );
}

/// Shown wherever a screen renders seeded reference content. It is deliberately
/// conspicuous: preview content must never be mistaken for live data.
class DemoPreviewBanner extends StatelessWidget {
  const DemoPreviewBanner({super.key});

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
        decoration: BoxDecoration(
          color: AppColors.accent100,
          border: Border.all(color: AppColors.accent300),
        ),
        child: const Row(
          children: [
            Icon(Icons.visibility_outlined, size: 16, color: AppColors.accent800),
            SizedBox(width: 8),
            Expanded(
              child: Text(
                'Development preview data — this area connects when its API is implemented.',
                style: TextStyle(fontSize: 12, color: AppColors.accent800),
              ),
            ),
          ],
        ),
      );
}

/// The honest state a production build shows for a screen whose backend does not
/// exist yet.
class PreviewUnavailable extends StatelessWidget {
  final String message;

  const PreviewUnavailable({super.key, required this.message});

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Ph(label: 'no data', width: 96, height: 96),
              const SizedBox(height: 16),
              const Heading('Not available yet.',
                  fontSize: 20, textAlign: TextAlign.center),
              const SizedBox(height: 6),
              FNote(message, textAlign: TextAlign.center),
            ],
          ),
        ),
      );
}
