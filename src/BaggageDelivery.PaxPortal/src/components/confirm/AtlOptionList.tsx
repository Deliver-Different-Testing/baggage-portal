import { memo } from 'react'
import { Radio, Stack } from '@mantine/core'
import type { AtlOption } from '../../api/client'

export const AtlOptionList = memo(({
                                     options,
                                     selectedId,
                                     onSelect,
                                   }: {
  options: AtlOption[]
  selectedId: number | null
  onSelect: (id: number) => void
}) => (
    <Radio.Group
        value={selectedId === null ? '' : String(selectedId)}
        onChange={(v) => onSelect(Number(v))}
    >
      <Stack
          gap={6}
          style={options.length > 6 ? {maxHeight: 260, overflowY: 'auto', paddingRight: 8} : undefined}
      >
        {options.map((opt) => (
            <Radio key={opt.id} value={String(opt.id)} label={opt.name}/>
        ))}
      </Stack>
    </Radio.Group>
))
