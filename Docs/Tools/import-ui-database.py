#!/usr/bin/env python3
"""Export the agreed UI workbook without Excel dependencies; never writes the source workbook."""
import argparse
import json
import re
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET

NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
PROJECT = Path(__file__).resolve().parents[2]
CHAPTER_IDS = [['001', '002'], *[[str(c * 100 + n) for n in range(1, 5)] for c in range(1, 5)]]

def read_sheets(path):
    with ZipFile(path) as z:
        strings = []
        if 'xl/sharedStrings.xml' in z.namelist():
            strings = [''.join(e.itertext()) for e in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si', NS)]
        rel = {e.attrib['Id']: e.attrib['Target'] for e in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        result = {}
        for sheet in ET.fromstring(z.read('xl/workbook.xml')).findall('s:sheets/s:sheet', NS):
            target = rel[sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']]
            target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            rows = []
            for row in ET.fromstring(z.read(target)).findall('s:sheetData/s:row', NS):
                values = {}
                for cell in row.findall('s:c', NS):
                    col = re.match('[A-Z]+', cell.attrib['r'])[0]
                    v = cell.find('s:v', NS)
                    value = v.text if v is not None else None
                    if cell.attrib.get('t') == 's' and value is not None: value = strings[int(value)]
                    if cell.attrib.get('t') == 'inlineStr': value = ''.join(cell.find('s:is', NS).itertext())
                    values[col] = value
                if any(v is not None for v in values.values()): rows.append(values)
            if rows:
                headers = rows[0]
                result[sheet.attrib['name']] = [{label: row.get(col) for col, label in headers.items() if label} for row in rows[2:]]
        return result

def export(source):
    sheets = read_sheets(source)
    required = ['Dialogue', 'DialogueGroups', 'Archives', 'Chapters']
    if any(n not in sheets for n in required): raise ValueError('Missing required sheets')
    chapter_rows = sorted(sheets['Chapters'], key=lambda r: int(r['order']))
    level_rows = sheets.get('Levels', [])
    # The completed workbook owns membership/order. Retain the agreed draft mapping only
    # for converting the earlier incomplete workbook with its one placeholder level.
    chapter_ids = CHAPTER_IDS
    if len(level_rows) > 1:
        chapter_ids = [[r['levelId'] for r in sorted(level_rows, key=lambda r: int(r['order'])) if r.get('chapterId') == c['chapterId']] for c in chapter_rows]
        if any(not ids for ids in chapter_ids): raise ValueError('Every published chapter requires levels')
        flat = [i for ids in chapter_ids for i in ids]
        if len(flat) != len(set(flat)) or len(flat) != len(level_rows): raise ValueError('Duplicate level ID or unknown chapter')
    levels = []
    by_id = {r.get('levelId'): r for r in level_rows}
    for ids in chapter_ids:
        for level_id in ids:
            path = PROJECT / 'Assets/StreamingAssets/Levels' / (level_id + '.json')
            data = json.loads(path.read_text())
            if data['id'] != level_id: raise ValueError('ID differs from filename: ' + level_id)
            row = by_id.get(level_id, {})
            levels.append({'id': level_id, 'title': row.get('title') or data.get('name') or level_id, 'description': row.get('description') or ''})
    chapter_source = {r['chapterId']: r for r in sheets['Chapters']}
    names = ['新手教程', '基础死亡规则与演化限制', '分裂细胞', '区域繁殖与额外目标', '综合考验']
    positions = [(.40, .35), (.60, .53), (.52, .61), (.59, .73), (.53, .91)]
    chapters = []
    for i, ids in enumerate(chapter_ids):
        chapter_id = 'chapter_%02d' % (i + 1); r = chapter_source[chapter_id]
        def coordinate(key, default):
            val = r.get(key)
            return default if not val or '待' in val else float(val)
        chapters.append({'id': chapter_id, 'title': r.get('title') or names[i], 'bodyPart': r.get('bodyPart') or '',
                         'archiveId': r.get('archiveId') or '', 'bodyPosition': {'x': coordinate('nodeX', positions[i][0]), 'y': coordinate('nodeY', positions[i][1])}, 'levelIds': ids})
    groups = []
    for r in sheets['DialogueGroups']:
        group_id = r['dialogueId']
        if not group_id: continue
        level_id = r.get('levelId') or ''
        if not level_id or '待填写' in level_id or '最终章最后一关' in level_id:
            chapter = int((r.get('chapterId') or group_id[:10]).split('_')[1])
            level_id = chapter_ids[chapter - 1][-1] if '_last_' in group_id else chapter_ids[chapter - 1][int(r['chapterLevelOrder']) - 1]
        if level_id not in {l['id'] for l in levels}: raise ValueError('Unknown level binding: ' + level_id)
        if r['kind'] not in ['Story', 'Tutorial'] or r['timing'] not in ['BeforeLevel', 'AfterWin']: raise ValueError('Invalid dialogue enum: ' + group_id)
        groups.append({'dialogueId': group_id, 'levelId': level_id, 'kind': r['kind'], 'timing': r['timing'], 'order': int(r['order'])})
    if len({r['dialogueId'] for r in groups}) != len(groups): raise ValueError('Duplicate group ID')
    lines = []
    for r in sheets['Dialogue']:
        if not r.get('lineId'): continue
        lines.append({k: (int(r[k]) if k == 'order' else r.get(k) or '') for k in ['lineId', 'dialogueId', 'order', 'speakerName', 'portraitId', 'text']})
    if len({r['lineId'] for r in lines}) != len(lines): raise ValueError('Duplicate line ID')
    for g in groups:
        members = sorted((r for r in lines if r['dialogueId'] == g['dialogueId']), key=lambda r: r['order'])
        if not members or [r['order'] for r in members] != list(range(1, len(members) + 1)): raise ValueError('Missing/duplicate line order: ' + g['dialogueId'])
    if any(r['dialogueId'] not in {g['dialogueId'] for g in groups} or not r['text'] for r in lines): raise ValueError('Invalid dialogue content')
    archives = [{k: r.get(k) or '' for k in ['archiveId', 'title', 'body', 'illustrationId', 'caption']} for r in sheets['Archives'] if r.get('archiveId')]
    for a in archives:
        if not a['title']:
            match = re.search('【([^】]+)】', a['body']); a['title'] = match[1] if match else a['archiveId']
    if any(c['archiveId'] and c['archiveId'] not in {a['archiveId'] for a in archives} for c in chapters): raise ValueError('Unknown archive binding')
    output = PROJECT / 'Assets/Resources/Campaign/Narrative.json'
    output.write_text(json.dumps(dict(lines=lines, groups=groups, archives=archives, chapters=chapters, levels=levels), ensure_ascii=False, indent=2) + '\n')
    print('Exported: %d chapters, %d levels, %d groups, %d lines, %d archives' % (len(chapters), len(levels), len(groups), len(lines), len(archives)))
    print('Workbook level IDs preserved; any draft group positions resolved against chapter membership. Source workbook unchanged.')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('source', type=Path); args = parser.parse_args()
    export(args.source)
