# Lease-end deposit estimate

The lease details and history views calculate deposit interest using the existing
annual percentage rate, compounded monthly:

`deposit × (1 + annualRatePercent / 1200) ^ months`

`months` uses actual elapsed milliseconds between the lease dates divided by
`365.25 / 12` days. Fractional periods use the same compound exponent. The result
is rounded to cents once at the end. Missing rates or invalid dates produce no
estimate; an explicitly entered zero rate returns the original deposit.

Monthly compounding is the implementation assumption pending confirmation of the
agency's required convention. This is an estimate, not a record of payment or
actual bank interest. Expired leases are not labelled as already paid.
