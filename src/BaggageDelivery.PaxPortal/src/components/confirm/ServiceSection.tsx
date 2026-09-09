import {memo, useRef, type KeyboardEvent} from 'react'
import {Box, Button, Center, Group, Loader, Stack, Text} from '@mantine/core'
import {FormSection} from './FormSection'
import {tokens} from '../../styles/mantineTheme'
import type {AvailableService} from '../../api/client'

function windowLabel(service: AvailableService): string | null {
    if (!service.bookDateUtc) return null
    const at = new Date(service.bookDateUtc)
    if (Number.isNaN(at.getTime())) return null
    return at.toLocaleString(undefined, {
        weekday: 'short',
        day: 'numeric',
        month: 'short',
        hour: 'numeric',
        minute: '2-digit',
    })
}

export const ServiceOption = memo(({
                                       service,
                                       selected,
                                       onSelect,
                                   }: {
    service: AvailableService
    selected: boolean
    onSelect: (jobTypeId: number) => void
}) => {
    const scheduled = windowLabel(service)

    return (
        <Box
            role="radio"
            aria-checked={selected}
            tabIndex={selected ? 0 : -1}
            className="pax-slot"
            onClick={() => onSelect(service.jobTypeId)}
            onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault()
                    onSelect(service.jobTypeId)
                }
            }}
            style={{
                borderColor: selected
                    ? 'var(--mantine-color-brand-filled)'
                    : 'var(--mantine-color-default-border)',
                backgroundColor: selected
                    ? 'var(--mantine-color-brand-light)'
                    : 'var(--dd-surface-container)',
            }}
        >
            <Center
                w={20}
                h={20}
                style={{
                    borderRadius: '50%',
                    border: `2px solid ${selected ? 'var(--mantine-color-brand-filled)' : 'var(--mantine-color-default-border)'}`,
                    flexShrink: 0,
                }}
            >
                {selected && (
                    <Box
                        w={10}
                        h={10}
                        style={{
                            borderRadius: '50%',
                            backgroundColor: 'var(--mantine-color-brand-filled)',
                        }}
                    />
                )}
            </Center>
            <Box style={{flex: 1, minWidth: 0}}>
                <Text size="md" fw={700}>
                    {service.name}
                </Text>
                {service.description && (
                    <Text size="sm" c="dimmed">
                        {service.description}
                    </Text>
                )}
                {scheduled && (
                    <Text style={{...tokens.type.eyebrow, color: 'var(--dd-on-brand-tint)'}}>
                        {scheduled}
                    </Text>
                )}
            </Box>
        </Box>
    )
})

export const ServiceSection = memo(function ServiceSection({
                                   services,
                                   selectedId,
                                   loading,
                                   error,
                                   fieldError,
                                   onSelect,
                                   onRetry,
                               }: {
    services: AvailableService[]
    selectedId: number | null
    loading: boolean
    error: boolean
    fieldError?: string
    onSelect: (jobTypeId: number) => void
    onRetry: () => void
}) {
    const ref = useRef<HTMLDivElement>(null)

    const moveTo = (index: number) => {
        const target = services[index]
        if (!target) return
        onSelect(target.jobTypeId)
        ref.current?.querySelectorAll<HTMLElement>('[role="radio"]')[index]?.focus()
    }

    const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
        const current = services.findIndex((s) => s.jobTypeId === selectedId)
        if (current === -1) return
        switch (e.key) {
            case 'ArrowDown':
            case 'ArrowRight':
                e.preventDefault()
                moveTo((current + 1) % services.length)
                break
            case 'ArrowUp':
            case 'ArrowLeft':
                e.preventDefault()
                moveTo((current - 1 + services.length) % services.length)
                break
            case 'Home':
                e.preventDefault()
                moveTo(0)
                break
            case 'End':
                e.preventDefault()
                moveTo(services.length - 1)
                break
        }
    }

    return (
        <FormSection
            title="Delivery service"
            subtitle="What we can offer to your new address"
            emphasis="primary"
        >
            {loading && (
                <Group gap="sm" align="center" py="xs">
                    <Loader size={18}/>
                    <Text size="sm" c="dimmed">
                        Checking what we can deliver to that address…
                    </Text>
                </Group>
            )}
            {fieldError && (
                <Text size="sm" c="red" mb="xs">
                    {fieldError}
                </Text>
            )}
            {error && (
                <Stack gap="xs" align="flex-start">
                    <Text size="sm">We couldn&apos;t check that address just now.</Text>
                    <Button variant="light" size="xs" onClick={onRetry}>
                        Try again
                    </Button>
                </Stack>
            )}
            {!loading && !error && services.length > 0 && (
                <Stack
                    ref={ref}
                    id="pax-delivery-service"
                    tabIndex={-1}
                    gap="xs"
                    role="radiogroup"
                    aria-label="Delivery service"
                    onKeyDown={handleKeyDown}
                >
                    {services.map((service) => (
                        <ServiceOption
                            key={service.jobTypeId}
                            service={service}
                            selected={service.jobTypeId === selectedId}
                            onSelect={onSelect}
                        />
                    ))}
                </Stack>
            )}
        </FormSection>
    )
})
