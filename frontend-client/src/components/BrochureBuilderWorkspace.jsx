import { useState, useEffect, useRef } from 'react';
import { Sparkles, Printer, Image, CheckCircle2, User, Phone, Mail, Building2, MapPin, Layers, Loader2, ArrowLeft, AlertTriangle, Plus, X } from 'lucide-react';
import apiClient from '../services/apiClient.js';
import { propertyService } from '../services/propertyService.js';
import { crmService } from '../services/crmService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useSubscription } from '../contexts/SubscriptionContext.jsx';

export default function BrochureBuilderWorkspace({ dealData, onBack }) {
  const { activeProfile } = useAuth();

  const [properties, setProperties] = useState([]);
  const [selectedUnits, setSelectedUnits] = useState([]);
  const [unitImageMap, setUnitImageMap] = useState({});
  const [companyInfo, setCompanyInfo] = useState({ name: '', logoUrl: '' });
  const [clientInfo, setClientInfo] = useState({ name: '', email: '', phone: '' });
  const [cardId, setCardId] = useState(null);
  const [cardStatus, setCardStatus] = useState('idle');
  const [isCompilingPdf, setIsCompilingPdf] = useState(false);
  const [isLoadingAsset, setIsLoadingAsset] = useState(true);
  const [feedbackMessage, setFeedbackMessage] = useState('');
  const [addPropertyId, setAddPropertyId] = useState('');

  const pollRef = useRef(null);

  const [dealBrokers, setDealBrokers] = useState([]);
  const brokerProfile = {
    fullName: dealBrokers[0]?.fullName || 'Unassigned broker',
    emailAddress: dealBrokers[0]?.email || '', phoneNumber: dealBrokers[0]?.phone || '',
    companyReferenceNumber: `DEAL-${dealData?.dealId || ''}`, companyName: companyInfo.name || 'Agency'
  };
  useEffect(() => {
    if (!dealData?.dealId) return;
    let mounted = true;
    apiClient.get(`/crm/deals/${dealData.dealId}/presentation`).then(({ data }) => {
      if (!mounted) return;
      setDealBrokers(data.brokers || []);
      setCompanyInfo({ name: data.agencyName, logoUrl: data.agencyLogoUrl || '' });
    }).catch(error => { if (mounted) setFeedbackMessage(error.message); });
    return () => { mounted = false; };
  }, [dealData?.dealId]);

  const subscription = useSubscription();
  const isOnTrial = subscription?.isTrial === true;
  const trialExpiresAt = subscription?.trialEndsAt;
  const isTrialExpired = isOnTrial && trialExpiresAt && new Date(trialExpiresAt) < new Date();

  const formatTrialExpiry = (expiry) => {
    if (!expiry) return '';
    try {
      return new Date(expiry).toLocaleDateString('en-ZA', { year: 'numeric', month: 'short', day: 'numeric' });
    } catch { return ''; }
  };

  const allUnits = properties.flatMap(p =>
    (p.subUnits || []).map(u => ({
      ...u,
      propertyId: p.propertyId,
      propertyDescription: p.propertyDescription,
      propertyAddress: p.completeAddress,
      propertyLatitude: p.latitude,
      propertyLongitude: p.longitude,
      marketClass: p.marketClass,
      salePrice: p.salePrice,
      rentalRate: u.rentalRate,
      rentEscalationPercentage: u.rentEscalationPercentage,
      leaseTermMonths: u.leaseTermMonths,
      isCurrentlyVacant: u.isCurrentlyVacant,
      propertyType: p.propertyTypeDetails?.name || '',
      valuationPrice: p.valuationPrice,
      erfSpatialSize: p.erfSpatialSize,
      titleDeedType: p.titleDeedType,
      portfolioZoning: p.portfolioZoning,
      isForSale: p.isForSale,
      isForRent: p.isForRent
    }))
  );

  const loadProperty = async (propertyId) => {
    try {
      const data = await propertyService.getPropertyById(propertyId);
      setProperties(prev => {
        const exists = prev.find(p => p.propertyId === data.propertyId);
        return exists ? prev.map(p => p.propertyId === data.propertyId ? data : p) : [...prev, data];
      });
    } catch {
      setFeedbackMessage(`Failed to load property ${propertyId}.`);
    }
  };

  useEffect(() => {
    if (!dealData?.dealId) return;
    (async () => {
      try {
        const card = await crmService.getBrochureCardByDeal(dealData.dealId);
        if (card) {
          setCardId(card.cardId);
          setClientInfo({ name: card.clientName || '', email: card.clientEmail || '', phone: card.clientPhone || '' });
          setCardStatus((card.status || 'active').toLowerCase());
          if (Array.isArray(card.lines) && card.lines.length) {
            const unitIds = card.lines.map(l => l.unitId);
            setSelectedUnits(card.lines.map(l => ({
              propertyUnitId: l.unitId,
              propertyId: l.propertyId,
              unitNumber: l.unitNumber,
              unitDescription: l.unitDescription,
              propertyDescription: l.propertyDescription || '',
              netRentableAreaSqM: l.netRentableAreaSqM,
              rentalRate: 0,
              rentEscalationPercentage: 0,
              leaseTermMonths: 12,
              salePrice: 0,
              selectedImageRefs: l.selectedImageRefs || []
            })));
            const imgMap = {};
            card.lines.forEach(l => {
              imgMap[l.unitId] = l.selectedImageRefs || [];
            });
            setUnitImageMap(imgMap);
            const uniquePropertyIds = [...new Set(card.lines.map(l => l.propertyId))];
            for (const pid of uniquePropertyIds) {
              await loadProperty(pid);
            }
          }
        }
      } catch { /* no card yet — clean canvas */ }
    })();
  }, [dealData]);

  useEffect(() => {
    const fetchTargetAssetDetails = async () => {
      setIsLoadingAsset(true);
      try {
        if (!dealData?.linkedPropertyId) throw new Error('The deal has no linked property. Link a property before creating a brochure.');
        await loadProperty(dealData.linkedPropertyId);
      } catch {
        setFeedbackMessage('Failed to pull asset metrics for proposal compiling.');
      } finally {
        setIsLoadingAsset(false);
      }
    };
    fetchTargetAssetDetails();
  }, [dealData]);

  const handleAddProperty = async (e) => {
    e.preventDefault();
    const pid = parseInt(addPropertyId, 10);
    if (!pid) return;
    await loadProperty(pid);
    setAddPropertyId('');
  };

  const handleRemoveProperty = (propertyId) => {
    setProperties(prev => prev.filter(p => p.propertyId !== propertyId));
    setSelectedUnits(prev => prev.filter(u => u.propertyId !== propertyId));
    setUnitImageMap(prev => {
      const next = { ...prev };
      Object.keys(next).forEach(key => {
        const unit = selectedUnits.find(u => u.propertyUnitId === parseInt(key, 10));
        if (unit && unit.propertyId === propertyId) delete next[key];
      });
      return next;
    });
  };

  const handleToggleUnit = (unit) => {
    if (selectedUnits.some(u => u.propertyUnitId === unit.propertyUnitId)) {
      setSelectedUnits(selectedUnits.filter(u => u.propertyUnitId !== unit.propertyUnitId));
    } else {
      if (selectedUnits.length >= 5) {
        alert("Presentation constraints limit the brochure to 5 curated property units per client deck.");
        return;
      }
      const enriched = { ...unit, selectedImageRefs: unitImageMap[unit.propertyUnitId] || [] };
      setSelectedUnits([...selectedUnits, enriched]);
      setUnitImageMap(prev => ({ ...prev, [unit.propertyUnitId]: prev[unit.propertyUnitId] || [] }));
    }
  };

  const handleRemoveUnit = async (unitId) => {
    const unit = selectedUnits.find(u => u.propertyUnitId === unitId);
    setSelectedUnits(selectedUnits.filter(u => u.propertyUnitId !== unitId));
    if (unit) {
      setUnitImageMap(prev => {
        const next = { ...prev };
        delete next[unitId];
        return next;
      });
    }
    if (cardId) {
      try { await crmService.removeBrochureCardLine(cardId, unitId); }
      catch (err) { setFeedbackMessage(err || 'Could not remove unit from card.'); }
    }
  };

  const handleToggleUnitImage = (unitId, imgUrl) => {
    setUnitImageMap(prev => {
      const current = prev[unitId] || [];
      const next = current.includes(imgUrl)
        ? current.filter(item => item !== imgUrl)
        : current.length >= 5 ? current : [...current, imgUrl];
      return { ...prev, [unitId]: next };
    });
    setSelectedUnits(prev => prev.map(u => u.propertyUnitId === unitId ? { ...u, selectedImageRefs: unitImageMap[unitId] || [] } : u));
  };

  const handleTriggerKafkaGeneration = async (e) => {
    e.preventDefault();
    if (isTrialExpired) {
      setFeedbackMessage('Your trial has expired. Upgrade to a paid plan to generate brochures.');
      return;
    }
    if (!selectedUnits.length) {
      setFeedbackMessage('Select at least one property unit to present to the client.');
      return;
    }
    if (!clientInfo.name.trim()) {
      setFeedbackMessage('Enter the client name before generating the brochure.');
      return;
    }
    setIsCompilingPdf(true);
    setFeedbackMessage('');
    setCardStatus('generating');

    try {
      const savedCard = await crmService.saveBrochureCard({
        dealId: dealData.dealId,
        clientName: clientInfo.name,
        clientEmail: clientInfo.email,
        clientPhone: clientInfo.phone,
        companyName: companyInfo.name,
        companyLogoUrl: companyInfo.logoUrl,
        units: selectedUnits.map(u => ({
          propertyId: u.propertyId,
          unitId: u.propertyUnitId,
          unitNumber: u.unitNumber,
          unitDescription: u.unitDescription,
          propertyDescription: u.propertyDescription,
          netRentableAreaSqM: u.netRentableAreaSqM,
          selectedImageRefs: unitImageMap[u.propertyUnitId] || []
        }))
      });
      const newCardId = savedCard?.cardId || cardId;
      setCardId(newCardId);

      const marketClasses = [...new Set(selectedUnits.map(u => u.marketClass))];
      const primaryMarketClass = marketClasses.includes('Rental') ? 'Rental' : 'Sale';

      const uniquePropertyIds = [...new Set(selectedUnits.map(u => u.propertyId))];
      const amenitiesByProperty = {};
      await Promise.all(uniquePropertyIds.map(async (pid) => {
        try {
          const data = await propertyService.getPropertyAmenities(pid);
          amenitiesByProperty[pid] = data.items || [];
        } catch {
          amenitiesByProperty[pid] = [];
        }
      }));

      const payloadEnvelope = {
        DealId: dealData.dealId || 0,
        BrokerName: brokerProfile.fullName,
        BrokerEmail: brokerProfile.emailAddress,
        BrokerPhone: brokerProfile.phoneNumber,
        CompanyName: companyInfo.name || brokerProfile.companyName,
        CompanyLogoUrl: companyInfo.logoUrl || '',
        ClientName: clientInfo.name,
        ClientEmail: clientInfo.email,
        ClientPhone: clientInfo.phone,
        MarketClass: primaryMarketClass,
        TenantInstallationAllowance: properties[0]?.tenantInstallationAllowance || 0,
        SubUnits: selectedUnits.map(u => ({
          PropertyId: u.propertyId,
          PropertyDescription: u.propertyDescription,
          Latitude: u.propertyLatitude,
          Longitude: u.propertyLongitude,
          UnitNumber: u.unitNumber,
          UnitDescription: u.unitDescription,
          NetRentableAreaSqM: u.netRentableAreaSqM,
          RentalRate: u.rentalRate,
          RentEscalationPercentage: u.rentEscalationPercentage,
          LeaseTermMonths: u.leaseTermMonths,
          SalePrice: u.salePrice,
          SelectedImageRefs: unitImageMap[u.propertyUnitId] || [],
          PropertyType: u.propertyType || '',
          ValuationPrice: u.valuationPrice || null,
          ErfSpatialSize: u.erfSpatialSize || '',
          TitleDeedType: u.titleDeedType || '',
          PortfolioZoning: u.portfolioZoning || '',
          IsForSale: u.isForSale || false,
          IsForRent: u.isForRent || false,
          IsCurrentlyVacant: u.isCurrentlyVacant || false,
          Amenities: (amenitiesByProperty[u.propertyId] || []).map(a => ({
            Name: a.AmenityName || a.name || '',
            Label: a.Label || a.label || '',
            BooleanValue: !!a.BooleanValue,
            NumberValue: a.NumberValue ?? null
          }))
        })),
        BrochureCardId: newCardId
      };

      await propertyService.dispatchBrochureGenerationTask(payloadEnvelope);
      setFeedbackMessage('Brochure request dispatched to Kafka. The worker is compiling the PDF...');

      pollRef.current = setInterval(async () => {
        try {
          const card = await crmService.getBrochureCardByDeal(dealData.dealId);
          if (!card) {
            clearInterval(pollRef.current);
            setCardStatus('generated');
            setIsCompilingPdf(false);
            setFeedbackMessage('✅ Brochure generated and saved to the server brochure folder. The card has been closed and the deal advanced to Proposal / Quote Sent.');
          } else if (card.status === 'Generated') {
            clearInterval(pollRef.current);
            setCardStatus('generated');
            setIsCompilingPdf(false);
            setFeedbackMessage('✅ Brochure generated. Card closed and deal advanced.');
          }
        } catch { /* transient — keep polling */ }
      }, 3000);

      setTimeout(() => {
        if (isCompilingPdf) {
          clearInterval(pollRef.current);
          setIsCompilingPdf(false);
          setCardStatus('failed');
          setFeedbackMessage('Generation timed out — see the worker logs. The card remains active and can be regenerated.');
        }
      }, 60000);
    } catch (err) {
      clearInterval(pollRef.current);
      setIsCompilingPdf(false);
      setCardStatus('failed');
      setFeedbackMessage(err || 'Failed to dispatch the background generation event.');
    }
  };

  if (isLoadingAsset) {
    return (
      <div className="py-20 flex flex-col items-center justify-center text-slate-500 gap-3">
        <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
        <span className="text-xs font-medium tracking-wide">Extracting property profile data maps...</span>
      </div>
    );
  }

  const activeUnitsCount = selectedUnits.length;

  return (
    <div className="space-y-6 animate-fade-in pb-12">

      {isOnTrial && !isTrialExpired && (
        <div className="flex items-start gap-3 rounded-2xl bg-amber-950/50 border border-amber-900 p-4">
          <AlertTriangle size={20} className="text-amber-400 shrink-0 mt-0.5" />
          <div>
            <p className="text-xs font-bold text-amber-300 uppercase tracking-wider">Trial Period Active</p>
            <p className="text-xs text-amber-200 mt-0.5">
              Your 14-day free trial expires on {formatTrialExpiry(trialExpiresAt)}.
              Upgrade to a paid plan to continue generating brochures after expiry.
            </p>
          </div>
        </div>
      )}
      {isTrialExpired && (
        <div className="flex items-start gap-3 rounded-2xl bg-red-950/50 border border-red-900 p-4">
          <AlertTriangle size={20} className="text-red-400 shrink-0 mt-0.5" />
          <div>
            <p className="text-xs font-bold text-red-300 uppercase tracking-wider">Trial Expired</p>
            <p className="text-xs text-red-200 mt-0.5">
              Your trial period has ended. Please upgrade to a paid plan to continue using this feature.
            </p>
          </div>
        </div>
      )}

      {/* 🧭 CONTROL NAVIGATION ACTION PANEL BAR ROW */}
      <div className="flex items-center justify-between border-b border-slate-800 pb-4">
        <div className="flex items-center gap-3">
          <button
            onClick={onBack}
            className="p-2 bg-slate-950 hover:bg-slate-800 border border-slate-800 rounded-xl text-slate-400 hover:text-white transition-colors cursor-pointer"
          >
            <ArrowLeft size={16} />
          </button>
          <div>
            <h2 className="text-xl font-black text-white tracking-tight">Brochure Generator Deck</h2>
            <p className="text-xs text-slate-400 font-medium mt-0.5">Asynchronous Event-Driven Proposal Platform Compiler</p>
          </div>
        </div>

        {cardStatus === 'generated' && (
          <span className="text-[10px] font-bold font-mono text-emerald-400 bg-emerald-950/40 px-2.5 py-1 rounded-lg border border-emerald-900">
            ✅ BROCHURE GENERATED
          </span>
        )}
        {cardStatus === 'generating' && (
          <span className="text-[10px] font-bold font-mono text-amber-400 bg-amber-950/40 px-2.5 py-1 rounded-lg border border-amber-900">
            ⏳ COMPILING VIA WORKER
          </span>
        )}

        <button
          onClick={handleTriggerKafkaGeneration}
          disabled={isCompilingPdf || cardStatus === 'generated' || isTrialExpired}
          className="bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 text-white text-xs font-bold py-2.5 px-4 rounded-xl shadow-lg transition-colors cursor-pointer flex items-center gap-2"
        >
          {isCompilingPdf ? <Loader2 size={14} className="animate-spin" /> : <Printer size={14} />}
          {isCompilingPdf ? 'Queue Processing...' : cardStatus === 'generated' ? 'Regenerate Brochure' : 'Compile Brochure Task'}
        </button>
      </div>

      {feedbackMessage && (
        <div className="p-4 bg-blue-950/40 border border-blue-900 text-blue-400 rounded-2xl text-xs font-medium text-center animate-fade-in flex items-center justify-center gap-2">
          <Sparkles size={14} className="animate-pulse" /> {feedbackMessage}
        </div>
      )}

      {/* 📋 CLIENT + COMPANY PRESENTATION DETAILS */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
        <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
          <User size={14} className="text-blue-400" /> Client & Company Presentation Details
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-3">
            <div>
              <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Client Name</label>
              <input type="text" value={clientInfo.name}
                onChange={(e) => setClientInfo({ ...clientInfo, name: e.target.value })}
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                placeholder="Attn: Client decision maker" />
            </div>
            <div>
              <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Client Email</label>
              <input type="email" value={clientInfo.email}
                onChange={(e) => setClientInfo({ ...clientInfo, email: e.target.value })}
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                placeholder="client@company.com" />
            </div>
            <div>
              <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Client Phone</label>
              <input type="tel" value={clientInfo.phone}
                onChange={(e) => setClientInfo({ ...clientInfo, phone: e.target.value })}
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                placeholder="+27 82 000 0000" />
            </div>
          </div>
          <div className="space-y-3">
            <div>
              <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Company Name (Cover Page)</label>
              <input type="text" value={companyInfo.name}
                onChange={(e) => setCompanyInfo({ ...companyInfo, name: e.target.value })}
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                placeholder="Agency / Company name" />
            </div>
            <div>
              <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Company Logo URL (optional)</label>
              <input type="url" value={companyInfo.logoUrl}
                onChange={(e) => setCompanyInfo({ ...companyInfo, logoUrl: e.target.value })}
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                placeholder="https://example.com/logo.png" />
            </div>
          </div>
        </div>
      </div>

      {/* 📊 GRID LAYOUT SPLIT PANELS */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

        {/* LEFT COLUMN PANEL: COVER SHEET PREVIEW BROKER META */}
        <div className="lg:col-span-1 space-y-4">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
              <User size={14} className="text-blue-400" /> Page 1: Broker Cover Summary
            </h3>

            <div className="bg-slate-950/80 border border-slate-800 rounded-xl p-4 space-y-3">
              <div className="flex items-center gap-3">
                {companyInfo.logoUrl ? (
                  <img src={companyInfo.logoUrl} alt="Company logo" className="w-10 h-10 rounded-xl object-cover border border-slate-800" onError={(e) => { e.target.style.display = 'none'; e.target.nextSibling.style.display = 'flex'; }} />
                ) : null}
                <div className={`w-10 h-10 rounded-xl bg-blue-600 font-black text-white items-center justify-center text-sm shadow-md ${companyInfo.logoUrl ? 'hidden' : 'flex'}`}>
                  {(companyInfo.name || brokerProfile.companyName).split(/\s+/).map((part) => part[0]).join('').slice(0, 3).toUpperCase()}
                </div>
                <div>
                  <h4 className="text-white font-bold text-sm tracking-tight">{companyInfo.name || brokerProfile.companyName}</h4>
                  <span className="text-[10px] font-mono text-slate-500">Ref: {brokerProfile.companyReferenceNumber}</span>
                </div>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-3 border-t border-slate-800 text-xs text-slate-300">
                {dealBrokers.length === 0 && <p>No broker assigned to this deal.</p>}
                {dealBrokers.map(broker => <section key={broker.brokerId} className="space-y-2">
                  <p className="font-semibold">{broker.fullName}</p><p>{broker.email}</p><p>{broker.phone}</p>
                </section>)}
              </div>
            </div>
          </div>

          {/* 🧾 SELECTED UNIT LINE-ITEMS SUMMARY (cart) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl space-y-3">
            <h3 className="text-[10px] font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
              <Layers size={14} className="text-blue-400" /> Curated Unit Selection ({activeUnitsCount}/5)
            </h3>
            {selectedUnits.length ? (
              <div className="space-y-2">
                {selectedUnits.map((u) => (
                  <div key={u.propertyUnitId} className="flex items-center justify-between p-2 bg-slate-950/40 border border-slate-800/80 rounded-lg text-xs">
                    <span className="text-blue-300 font-mono">{u.unitNumber} · {u.netRentableAreaSqM} m²</span>
                    <button
                      onClick={() => handleRemoveUnit(u.propertyUnitId)}
                      className="text-[10px] text-red-400 hover:text-red-300 cursor-pointer"
                      title="Remove this unit from the brochure deck"
                    >✕</button>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-[10px] text-slate-500 italic">No units selected yet.</p>
            )}
          </div>
        </div>

        {/* RIGHT COLUMN PANEL: PROPERTIES + UNIT SELECTION + IMAGES */}
        <div className="lg:col-span-2 space-y-4">

          {/* PROPERTY MANAGEMENT */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
              <Building2 size={14} className="text-blue-400" /> Included Properties ({properties.length})
            </h3>
            {properties.length === 0 && (
              <p className="text-xs text-slate-500 italic py-2">Loading properties...</p>
            )}
            <div className="space-y-3">
              {properties.map((p) => (
                <div key={p.propertyId} className="p-3 bg-slate-950/40 border border-slate-800/80 rounded-xl">
                  <div className="flex items-start justify-between">
                    <div>
                      <p className="text-white font-bold text-xs">{p.completeAddress}</p>
                      <p className="text-[10px] text-slate-500 mt-0.5">{p.propertyDescription || p.marketClass} · {p.subUnits?.length || 0} units</p>
                    </div>
                    <button onClick={() => handleRemoveProperty(p.propertyId)} className="text-[10px] text-red-400 hover:text-red-300 cursor-pointer">Remove</button>
                  </div>
                </div>
              ))}
            </div>
            <form onSubmit={handleAddProperty} className="flex gap-2">
              <input type="number" aria-label="Property ID to add" value={addPropertyId}
                onChange={(e) => setAddPropertyId(e.target.value)}
                className="flex-1 px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                placeholder="Add property by ID..." />
              <button type="submit" className="px-3 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold rounded-xl flex items-center gap-1 cursor-pointer">
                <Plus size={12} /> Add
              </button>
            </form>
          </div>

          {/* UNIT SELECTION */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <div className="flex justify-between items-center border-b border-slate-800/60 pb-2">
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                <Layers size={14} className="text-blue-400" /> Select Up To 5 Units Across All Properties
              </h3>
              <span className="text-[10px] font-bold font-mono text-slate-400 bg-slate-950 px-2 py-1 border border-slate-800 rounded-lg">
                Selected: {activeUnitsCount} / 5
              </span>
            </div>

            <div className="space-y-3">
              {allUnits.length > 0 ? (
                allUnits.map((unit) => {
                  const isChecked = selectedUnits.some(u => u.propertyUnitId === unit.propertyUnitId);
                  return (
                    <div key={`${unit.propertyId}-${unit.propertyUnitId}`} className={`p-3 bg-slate-950/40 border rounded-xl flex justify-between items-center text-xs transition-colors ${
                      isChecked ? 'border-blue-700 ring-1 ring-blue-700/30' : 'border-slate-800/80'
                    }`}>
                      <label className="flex items-center gap-3 cursor-pointer flex-1">
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={() => handleToggleUnit(unit)}
                          disabled={cardStatus === 'generated'}
                          className="w-4 h-4 rounded accent-blue-600 cursor-pointer"
                        />
                        <div>
                          <p className="font-bold text-white">{unit.unitNumber} — <span className="text-slate-400 font-medium">{unit.netRentableAreaSqM} m² NRA</span></p>
                          <p className="text-[11px] text-slate-500 mt-0.5 truncate max-w-lg">{unit.unitDescription}</p>
                          <p className="text-[10px] text-slate-600 mt-0.5">{unit.propertyAddress}</p>
                        </div>
                      </label>
                      <span className="text-[10px] font-bold px-2 py-0.5 rounded border border-slate-800 bg-slate-900 text-slate-400 font-mono">
                        {isChecked ? 'Curated' : 'Available'}
                      </span>
                    </div>
                  );
                })
              ) : (
                <p className="text-xs text-slate-500 italic py-2">No units available. Add a property to see its units.</p>
              )}
            </div>
          </div>

          {/* PER-UNIT IMAGE SELECTION */}
          {selectedUnits.length > 0 && (
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
                <Image size={14} className="text-blue-400" /> Per-Unit Image Selection
              </h3>
              <div className="space-y-4">
                {selectedUnits.map((u) => {
                  const property = properties.find(p => p.propertyId === u.propertyId);
                  const images = JSON.parse(property?.verificationDocumentsJson || '[]');
                  const selectedForUnit = unitImageMap[u.propertyUnitId] || [];
                  return (
                    <div key={u.propertyUnitId} className="p-3 bg-slate-950/40 border border-slate-800/80 rounded-xl space-y-2">
                      <p className="text-xs font-bold text-white">{u.unitNumber} · {u.propertyAddress}</p>
                      {images.length > 0 ? (
                        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-2">
                          {images.map((imgName, idx) => {
                            const isSelected = selectedForUnit.includes(imgName);
                            return (
                              <div key={idx} onClick={() => handleToggleUnitImage(u.propertyUnitId, imgName)}
                                className={`relative aspect-video rounded-lg border-2 overflow-hidden bg-slate-950 flex flex-col justify-between p-2 cursor-pointer select-none transition-all group ${
                                  isSelected ? 'border-blue-500 ring-2 ring-blue-500/20' : 'border-slate-800 hover:border-slate-700'
                                }`}>
                                <span className="text-[10px] font-mono text-slate-500 group-hover:text-slate-400 transition-colors truncate">{imgName}</span>
                                <div className="flex justify-end pt-3">
                                  <div className={`w-4 h-4 rounded-md border flex items-center justify-center transition-colors ${isSelected ? 'bg-blue-600 border-blue-500 text-white' : 'border-slate-700 bg-slate-900'}`}>
                                    {isSelected && <CheckCircle2 size={10} strokeWidth={3} />}
                                  </div>
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      ) : (
                        <p className="text-[10px] text-slate-500 italic">No images available for this property.</p>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

        </div>
      </div>

    </div>
  );
}
