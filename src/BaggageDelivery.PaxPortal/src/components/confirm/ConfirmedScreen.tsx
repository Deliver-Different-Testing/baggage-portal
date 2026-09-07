import { useLayoutEffect } from 'react'
import {
  Anchor,
  Badge,
  Box,
  Button,
  Card,
  Center,
  Container,
  Divider,
  List,
  Stack,
  Text,
  Title,
  Tooltip,
} from '@mantine/core'
import { ArrowRightIcon, CheckCircleIcon } from '../Icon'
import { DocketTile, Eyebrow, PunchedTag } from '../Docket'
import { DeliveryDocket } from '../DeliveryDocket'
import { FlightPathBackdrop } from '../FlightPathBackdrop'
import { PoweredByFooter } from '../PoweredByFooter'
import { onBrandScrim, tokens } from '../../styles/mantineTheme'
import type { AddressDto, AtlOption, BookingSummary, TimeSlot } from '../../api/client'

export const TRACKING_PENDING_HINT =
  'Tracking goes live once your bag is collected from the airport.'

const NEXT_STEPS = [
  'We collect your bag and deliver it to your address within the Delivery Window.',
  'We will text/email you when your bag is collected from the airport with a tracking link.',
  'You can track the driver from the airport to your address.',
]

export function ConfirmedScreen({
  summary,
  slot,
  address,
  passengerName,
  passengerPhone,
  passengerEmail,
  atlOption,
  accessNotes,
}: {
  summary: BookingSummary
  slot: Pick<TimeSlot, 'dayLabel' | 'label'> | undefined
  address: AddressDto
  passengerName: string
  passengerPhone: string
  passengerEmail: string
  atlOption: AtlOption | undefined
  accessNotes: string
}) {
  const trackingHref = summary.trackingUrl ?? null
  const trackingAvailable = trackingHref !== null

  useLayoutEffect(() => {
    window.scrollTo(0, 0)
  }, [])

  return (
    <Box mih="100vh" style={{ display: 'flex', flexDirection: 'column' }}>
      <Box
        px={tokens.hero.px}
        pt={tokens.hero.pt}
        pb={tokens.hero.pb}
        style={{
          backgroundColor: onBrandScrim.heroBg,
          color: onBrandScrim.text,
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        <FlightPathBackdrop />
        <Container
          size={tokens.hero.measure}
          px={0}
          style={{ textAlign: 'center', position: 'relative', zIndex: 1 }}
        >
          <Center
            w={80}
            h={80}
            mx="auto"
            mb="lg"
            style={{ borderRadius: '50%', backgroundColor: onBrandScrim.fill }}
          >
            <CheckCircleIcon size={48} color={onBrandScrim.text} />
          </Center>
          <Badge
            variant="transparent"
            mb="sm"
            style={{
              backgroundColor: onBrandScrim.fill,
              color: onBrandScrim.text,
              fontWeight: 700,
              letterSpacing: '0.08em',
            }}
          >
            Confirmed
          </Badge>
          <Title order={1} mb="xs" style={{ ...tokens.type.heroTitle, color: 'inherit' }}>
            You&apos;re all set
          </Title>
        </Container>
      </Box>

      <Container
        size={tokens.hero.measure}
        px={{ base: 12, sm: 0 }}
        mt={tokens.hero.overlap}
        pb={48}
        style={{ position: 'relative', flex: 1, width: '100%' }}
      >
        <Stack gap="md">
          <Card p="lg">
            <Stack gap="md">
              {summary.jobNumber && <PunchedTag label="Tracking number" value={summary.jobNumber} />}

              {slot && (
                <DocketTile label="Delivery window" variant="tint">
                  <Text fw={700} style={{ fontSize: tokens.type.figure, lineHeight: 1.15 }}>
                    {slot.dayLabel}
                  </Text>
                  <Text fw={600} style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {slot.label}
                  </Text>
                </DocketTile>
              )}

              <Divider />

              <DeliveryDocket
                address={address}
                passengerName={passengerName}
                passengerPhone={passengerPhone}
                passengerEmail={passengerEmail}
                atlOption={atlOption}
                accessNotes={accessNotes}
                fileReference={summary.fileReference}
              />
            </Stack>
          </Card>

          <Card p="lg">
            <Eyebrow>What happens next</Eyebrow>
            <List type="ordered" spacing={6} size="sm" mt={6} withPadding>
              {NEXT_STEPS.map((step) => (
                <List.Item key={step}>{step}</List.Item>
              ))}
            </List>
          </Card>

          <Tooltip
            label={TRACKING_PENDING_HINT}
            disabled={trackingAvailable}
            events={{ hover: true, focus: true, touch: true }}
            multiline
            w={240}
            withArrow
            position="top"
          >
            <Box>
              {trackingHref ? (
                <Button
                  component="a"
                  href={trackingHref}
                  size="lg"
                  fullWidth
                  rightSection={<ArrowRightIcon size={18} />}
                  style={tokens.button.primary}
                >
                  Track your delivery
                </Button>
              ) : (
                <Button
                  size="lg"
                  fullWidth
                  disabled
                  rightSection={<ArrowRightIcon size={18} />}
                  style={tokens.button.primary}
                >
                  Track your delivery
                </Button>
              )}
            </Box>
          </Tooltip>

          <Text size="sm" c="dimmed" ta="center">
            {TRACKING_PENDING_HINT}
          </Text>

          {summary.supportPhone && (
            <Text size="sm" c="dimmed" ta="center">
              Need to change something? Call{' '}
              <Anchor
                href={`tel:${summary.supportPhone.replace(/\s/g, '')}`}
                c="dimmed"
                underline="always"
              >
                {summary.supportPhone}
              </Anchor>
              {summary.jobNumber ? ` and quote tracking number ${summary.jobNumber}.` : '.'}
            </Text>
          )}
        </Stack>
      </Container>

      <PoweredByFooter />
    </Box>
  )
}
