#!/usr/bin/env python3
"""Rebuild the three numerical article figures from final-series raw CSV."""
from collections import defaultdict
from html import escape
from pathlib import Path
from statistics import median
import csv

BASE = Path(__file__).resolve().parents[1]
RUN = '20261104T075733840Z-abbb03c-ext4'
RAW = BASE / 'results' / 'final-series' / RUN / 'results'
OUT = BASE / 'docs' / 'figures'
MEASUREMENTS = '20261104T075733840Z'
POLAR, SQLITE = '#254766', '#9b6233'


def measurement(experiment, engine, n, phase, metric):
    values = []
    with (RAW / (experiment + '.raw.csv')).open(newline='') as file:
        for row in csv.DictReader(file):
            if (row['engine'] == engine and int(row['setup_rows']) == n
                    and row['phase'] == phase and row['metric'] == metric):
                values.append(float(row['value']))
    if not values:
        raise ValueError(f'Missing {experiment} / {engine} / {n} / {phase} / {metric}')
    return median(values)


def chart(name, header, unit, grouped, maximum, step):
    # Grouped bars share the same linear axis, starting at zero.
    width, left, right, top, group_height = 975, 240, 115, 110, 86
    axis_width = width - left - right
    height = top + len(grouped) * group_height + 95
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img">',
             f'<title>{escape(header)}</title>',
             '<desc>Медианы отдельных измеряемых стадий Polar.DB и SQLite. Линейная шкала начинается с нуля. Независимые повторения полной серии не выполнялись.</desc>',
             '<rect width="100%" height="100%" fill="white"/>',
             '<style>text{font-family:"DejaVu Sans",Arial,sans-serif;fill:#222;font-size:14px}.head{font-weight:600;font-size:19px}.muted{fill:#565656;font-size:12px}</style>',
             f'<text class="head" x="28" y="30">{escape(header)}</text>',
             f'<rect x="{left}" y="50" width="17" height="14" fill="{POLAR}"/>',
             f'<text x="{left+23}" y="62">Polar.DB</text>',
             f'<rect x="{left+180}" y="50" width="17" height="14" fill="{SQLITE}"/>',
             f'<text x="{left+203}" y="62">SQLite</text>']
    grid_bottom = top + len(grouped) * group_height - 6
    ticks = int(round(maximum / step))
    for i in range(ticks + 1):
        value = i * step
        x = left + axis_width * value / maximum
        parts.append(f'<line x1="{x:.1f}" x2="{x:.1f}" y1="{top-5}" y2="{grid_bottom}" stroke="#e3e3e3"/>')
        parts.append(f'<text class="muted" text-anchor="middle" x="{x:.1f}" y="{grid_bottom+25}">{value:g}</text>')
    parts.append(f'<text text-anchor="middle" x="{left+axis_width/2}" y="{grid_bottom+48}">{escape(unit)}</text>')
    for index, (label, polar, sqlite) in enumerate(grouped):
        y = top + index * group_height
        parts.append(f'<text x="{left-14}" y="{y+30}" text-anchor="end">{escape(label)}</text>')
        for offset, val, color in [(8, polar, POLAR), (39, sqlite, SQLITE)]:
            length = axis_width * val / maximum
            parts.append(f'<rect x="{left}" y="{y+offset}" width="{length:.2f}" height="22" fill="{color}"/>')
            display = f'{val:.3f}'.replace('.', ',')
            parts.append(f'<text x="{left+length+8:.2f}" y="{y+offset+16}">{display}</text>')
    parts.append(f'<text class="muted" x="28" y="{height-13}">Источник: серия {MEASUREMENTS}; медианы исходных CSV, 5 000 000 записей.</text>')
    parts.append('</svg>')
    (OUT / (name + '.svg')).write_text('\n'.join(parts) + '\n')



def reopen_stage_chart():
    """Show the measured query-ready stage medians without adding them together."""
    stages = [
        ('Создание строкового индекса (construct)', 'construct-external-3'),
        ('Обновление строкового индекса (Refresh)', 'refresh-external-3'),
        ('Обновление индекса Guid (Refresh)', 'refresh-external-2'),
        ('Обновление индекса int (Refresh)', 'refresh-external-0'),
        ('Обновление индекса long (Refresh)', 'refresh-external-1'),
        ('Обновление первичного индекса (Refresh)', 'refresh-primary'),
    ]
    left, right, width = 435, 120, 1140
    plot_width = width - left - right
    top, row_height = 90, 53
    bottom = top + len(stages) * row_height
    height = bottom + 90
    maximum = 5
    lines = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img">',
             '<title>Диагностические стадии повторного открытия Polar.DB на 5 млн записей</title>',
             '<desc>Шесть стадий полного цикла повторного открытия (query-ready reopen) с последующим закрытием. Медианы времени, шкала от нуля; суммы медиан стадий не равны медиане цикла.</desc>',
             '<rect width="100%" height="100%" fill="white"/>',
             '<style>text{font-family:"DejaVu Sans",Arial,sans-serif;fill:#222;font-size:14px}.head{font-weight:600;font-size:19px}.muted{fill:#565656;font-size:12px}</style>',
             '<text class="head" x="28" y="30">Стадии повторного открытия (query-ready reopen), 5 млн записей</text>']
    for tick in range(maximum + 1):
        x = left + tick * plot_width / maximum
        lines.append(f'<line x1="{x:.2f}" y1="{top-10}" x2="{x:.2f}" y2="{bottom}" stroke="#e3e3e3"/>')
        lines.append(f'<text class="muted" text-anchor="middle" x="{x:.2f}" y="{bottom+25}">{tick}</text>')
    lines.append(f'<text text-anchor="middle" x="{left+plot_width/2:.2f}" y="{bottom+48}">Медиана времени стадии, с</text>')
    for i, (label, stage) in enumerate(stages):
        v = measurement('reopen-only', 'polar-db-current', 5000000, '',
                        'reopen/query-ready/' + stage) / 1000
        y = top + i * row_height
        color = POLAR if stage.endswith('-3') else '#7b8790'
        lines.append(f'<text x="{left-14}" y="{y+25}" text-anchor="end">{escape(label)}</text>')
        length = plot_width * v / maximum
        lines.append(f'<rect x="{left}" y="{y+9}" width="{length:.2f}" height="26" fill="{color}"/>')
        display = f'{v:.3f}'.replace('.', ',')
        lines.append(f'<text x="{left+length+9:.2f}" y="{y+27}">{display}</text>')
    lines.append(f'<text class="muted" x="28" y="{height-14}">Источник: серия {MEASUREMENTS}; 30 повторов каждой стадии; медианы не суммируются.</text>')
    lines.append('</svg>')
    (OUT / 'reopen-stages-5m.svg').write_text('\n'.join(lines) + '\n')

def main():
    phase = 'Hot after file and lookup warmup'
    names = [('Целое число 32 бита (int)', 'int'), ('Целое число 64 бита (long)', 'long'), ('Идентификатор (Guid)', 'guid'), ('Строка (string)', 'string')]
    primary = []
    for label, ident in names:
        experiment = 'pk-' + ident + '-lookup'
        p = measurement(experiment, 'polar-db-current', 5000000, phase, 'lookup-batch-average') * 1000
        s = measurement(experiment, 'sqlite', 5000000, phase, 'lookup-batch-average') * 1000
        primary.append((label, p, s))
    chart('primary-lookup-5m', 'Первичный поиск после разогрева, 5 млн записей',
          'Медиана пакетного среднего, мкс/запрос', primary, 12, 2)
    experiment = 'build-external-indexes-only'
    p = measurement(experiment, 'polar-db-current', 5000000, '', 'build') / 1000
    s = measurement(experiment, 'sqlite', 5000000, '', 'build') / 1000
    chart('external-build-5m', 'Построение четырёх внешних индексов, 5 млн',
          'Медиана построения (Build), с', [('4 индекса', p, s)], 15, 3)
    experiment = 'traversal-only'
    p = measurement(experiment, 'polar-db-current', 5000000, '', 'full logical traversal') / 1000
    s = measurement(experiment, 'sqlite', 5000000, '', 'full logical traversal') / 1000
    chart('traversal-5m', 'Полный логический обход, 5 млн записей',
          'Медиана полного обхода, с', [('5 млн строк', p, s)], 9, 3)
    reopen_stage_chart()


if __name__ == '__main__':
    main()
