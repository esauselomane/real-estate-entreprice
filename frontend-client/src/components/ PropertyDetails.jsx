import React, { useState } from 'react';
import { ArrowLeft, MapPin, FileText, User, Home, CheckCircle2, ExternalLink, Image } from 'lucide-react';

export default function PropertyDetails({ propertyId, onBack, onNavigateToClient }) {
  // Local state tracking for the uploaded image file blob path
  const [imagePreview, setImagePreview] = useState(null);

  // Mock database repository containing distinct residential and commercial assets
  const propertyDatabase = {
    1: {
      address: "42 Beach Road, Green Point",
      erfSize: "450 m²",
      type: "Freehold",
      zoning: "Residential",
      price: 4500000,
      market: "Sale",
      ownerId: "O-991",
      ownerName: "Arthur Pendelton",
      tenantId: null,
      tenantName: "None (Vacant on transfer)",
      documents: ["Title_Deed_GP42.pdf", "Municipal_Zoning_Cert.pdf"],
      platforms: [
        { name: "Property24", checked: true, id: "P24-99812", link: "https://property24.com" },
        { name: "PrivateProperty", checked: false, id: null, link: null },
        { name: "Gumtree", checked: true, id: "GT-10492", link: "https://gumtree.co.za" }
      ]
    },
    2: {
      address: "105 Rivonia Road, Sandton",
      erfSize: "1,200 m²",
      type: "Sectional Title",
      zoning: "Commercial",
      price: 65000,
      market: "Rental",
      ownerId: "O-223",
      ownerName: "Vanguard Holdings Ltd",
      tenantId: "T-402",
      tenantName: "Apex Software Solutions",
      documents: ["Commercial_Lease_Agreement.pdf", "Fire_Safety_Cert_2026.pdf"],
      platforms: [
        { name: "Property24", checked: false, id: null, link: null },
        { name: "PrivateProperty", checked: true, id: "PP-33214", link: "https://privateproperty.co.za" },
        { name: "Gumtree", checked: false, id: null, link: null }
      ]
    }
  };

  // Extract the specific asset profile configuration mapping matching our id parameter
  const property = propertyDatabase[propertyId] || propertyDatabase[1];

  // Simulated browser image file upload handler mocking asynchronous streaming to storage disk clusters
  const handleImageUpload = (e) => {
    const file = e.target.files[0];
    if (file) {
      setImagePreview(URL.createObjectURL(file));
      alert("Image payload successfully broadcasted to Apache Kafka stream. Background .NET Core task worker will now read this and safely downsample into Thumb, Medium, and Large files directly on disk arrays.");
    }
  };

  return (
    <div className="space-y-6 animate-fade-in">
      
      {/* 🧭 Top Back Row Control Handle */}
      <div className="flex items-center justify-between">
        <button 
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-semibold text-slate-400 hover:text-white bg-slate-900 border border-slate-800 px-4 py-2 rounded-xl transition-colors cursor-pointer"
        >
          <ArrowLeft size={14} /> Back to Asset Registry
        </button>
        <span className="text-xs font-mono text-slate-500">Asset Record Reference: ID-00{propertyId}</span>
      </div>

      {/* Main Multi-Column Split Workspace Frame Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* 🏢 LEFT & CENTER WORKSPACE SECTION: Attributes, Map Layer Canvas, and Image Uploader */}
        <div className="lg:col-span-2 space-y-6">
          
          {/* Core Property Attributes Layout Box */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h3 className="text-xl font-black text-white mb-1 tracking-tight">{property.address}</h3>
            <p className="text-xs text-blue-400 font-medium mb-6 uppercase tracking-wider">{property.zoning} Portfolio Domain</p>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 p-4 bg-slate-950/60 rounded-xl border border-slate-800/80 mb-6">
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">Erf Spatial Size</span>
                <span className="text-sm font-bold text-white mt-0.5 block">{property.erfSize}</span>
              </div>
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">Tenure Type</span>
                <span className="text-sm font-bold text-white mt-0.5 block">{property.type}</span>
              </div>
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">Market Class</span>
                <span className="text-sm font-bold text-white mt-0.5 block">{property.market}</span>
              </div>
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">Financial Target</span>
                <span className="text-sm font-bold text-emerald-400 mt-0.5 block">R {property.price.toLocaleString()}</span>
              </div>
            </div>

            {/* 🗺️ Geospatial Positioning Vector Core Container Box Map Mockup */}
            <div className="h-64 w-full bg-slate-950 border border-slate-800 rounded-xl flex flex-col items-center justify-center p-4 text-center relative overflow-hidden">
              <MapPin size={32} className="text-blue-500 mb-2 animate-bounce" />
              <span className="text-sm font-bold text-slate-300">Spatial Geographic Engine Map Overlay</span>
              <span className="text-xs text-slate-500 mt-1 max-w-xs">{property.address} (GPS Coordinate System Registered)</span>
              <div className="absolute inset-0 bg-gradient-to-t from-slate-950 via-transparent to-transparent pointer-events-none" />
            </div>
          </div>

          {/* 🖼️ Media Image Submission & Resizing Area */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <Image size={16} className="text-blue-500" /> Asset Image Vault Gallery
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="border-2 border-dashed border-slate-800 hover:border-slate-700 rounded-xl p-6 flex flex-col items-center justify-center bg-slate-950/40 text-center relative min-h-[128px]">
                <input 
                  type="file" 
                  accept="image/*"
                  onChange={handleImageUpload}
                  className="absolute inset-0 opacity-0 cursor-pointer w-full h-full" 
                />
                <span className="text-xs text-slate-400 font-medium">Click or Drag to Upload Picture</span>
                <span className="text-[10px] text-slate-600 mt-1">Triggers sizing background automation run</span>
              </div>

              {imagePreview ? (
                <div className="bg-slate-950 border border-slate-800 rounded-xl p-2 flex items-center justify-center h-32 relative group">
                  <img src={imagePreview} alt="Upload thumb representation" className="h-full w-full object-cover rounded-lg" />
                  <span className="absolute bottom-2 right-2 bg-slate-900/90 text-[9px] px-1.5 py-0.5 rounded text-blue-400 border border-slate-700">Client Memory Variant</span>
                </div>
              ) : (
                <div className="bg-slate-950/20 border border-slate-800/80 rounded-xl flex items-center justify-center text-xs text-slate-600 italic">
                  No images uploaded yet
                </div>
              )}
            </div>
          </div>

        </div>
        {/* 📋 RIGHT WORKSPACE SIDEBAR PANEL: Entities Links, Deeds Docs, and Platform Sync Rows Checkboxes */}
        <div className="space-y-6">
          
          {/* Intercepted Linked Legal Entities Column Cards Box */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <User size={16} className="text-blue-500" /> Linked Legal Entities
            </h4>
            <div className="space-y-3">
              
              {/* Clickable Landowner Row Identifier */}
              <div className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl">
                <span className="text-[9px] text-amber-500 font-bold uppercase tracking-wider block">Registered Landowner</span>
                <button 
                  onClick={() => onNavigateToClient(property.ownerId, 'Owner', property.ownerName)}
                  className="text-sm font-bold text-white hover:text-blue-400 text-left mt-0.5 cursor-pointer underline decoration-dotted transition-colors block w-full"
                >
                  {property.ownerName}
                </button>
              </div>

              {/* Clickable Leaseholder Tenant Row Identifier */}
              <div className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl">
                <span className="text-[9px] text-blue-400 font-bold uppercase tracking-wider block">Leaseholder Tenant</span>
                {property.tenantId ? (
                  <button 
                    onClick={() => onNavigateToClient(property.tenantId, 'Tenant', property.tenantName)}
                    className="text-sm font-bold text-white hover:text-blue-400 text-left mt-0.5 cursor-pointer underline decoration-dotted transition-colors block w-full"
                  >
                    {property.tenantName}
                  </button>
                ) : (
                  <span className="text-sm text-slate-500 italic mt-0.5 block">{property.tenantName}</span>
                )}
              </div>

            </div>
          </div>

          {/* Verification Legal File Attachments Box */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <FileText size={16} className="text-blue-500" /> Deeds & Verification Vault
            </h4>
            <div className="space-y-2">
              {property.documents.map((doc, idx) => (
                <div key={idx} className="flex items-center gap-3 p-2.5 bg-slate-950 border border-slate-800 rounded-xl text-xs text-slate-300 group hover:border-slate-700 transition-colors">
                  <FileText size={14} className="text-slate-500 group-hover:text-blue-400" />
                  <span className="truncate flex-1 font-medium">{doc}</span>
                </div>
              ))}
            </div>
          </div>

          {/* External Platform Syndication Row Aggregators Box Tracker */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <Home size={16} className="text-blue-500" /> Portal Syndication Tracker
            </h4>
            <div className="space-y-3">
              {property.platforms.map((plat, idx) => (
                <div key={idx} className="flex items-center justify-between p-3 bg-slate-950/60 border border-slate-800 rounded-xl">
                  <div className="flex items-center gap-3">
                    <div className={`w-4 h-4 rounded flex items-center justify-center border transition-colors ${
                      plat.checked ? 'bg-blue-600 border-blue-500 text-white' : 'border-slate-700 bg-slate-950'
                    }`}>
                      {plat.checked && <CheckCircle2 size={12} />}
                    </div>
                    <span className="text-xs font-semibold text-slate-300">{plat.name}</span>
                  </div>

                  {plat.checked && plat.id ? (
                    <a 
                      href={plat.link} 
                      target="_blank" 
                      rel="noreferrer"
                      className="inline-flex items-center gap-1 text-[10px] text-blue-400 hover:text-blue-300 font-medium transition-colors"
                    >
                      Ref: {plat.id} <ExternalLink size={10} />
                    </a>
                  ) : (
                    <span className="text-[10px] text-slate-600 italic">Unlisted</span>
                  )}
                </div>
              ))}
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
